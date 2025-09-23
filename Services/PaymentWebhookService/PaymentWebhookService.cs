using E_Commerce.DtoModels.PaymentDtos;
using E_Commerce.Enums;
using E_Commerce.Interfaces;
using E_Commerce.Models;
using E_Commerce.Services.PaymentServices;
using E_Commerce.UOW;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json.Linq;

namespace E_Commerce.Services.PaymentWebhookService
{
	public interface IPaymentWebhookService
	{
		Task<bool> HandlePaymobAsync(PaymobWebhookDto dto, string receivedHmac);
	}

	public class PaymentWebhookService : IPaymentWebhookService
	{
		private readonly IUnitOfWork _unitOfWork;
		private readonly ILogger<PaymentWebhookService> _logger;
		private readonly IPaymentServices _paymentServices;
		private readonly IOrderServices _orderServices;
		private readonly IConfiguration _configuration;

		// Paymob fields order (as per docs)
		private static readonly string[] HmacFieldsOrder = new[]
		{
			"amount_cents", "created_at", "currency", "error_occured", "has_parent_transaction",
			"id", "integration_id", "is_3d_secure", "is_auth", "is_capture", "is_refunded",
			"is_standalone_payment", "is_voided", "order.id", "owner", "pending",
			"source_data.pan", "source_data.sub_type", "source_data.type", "success"
		};

		public PaymentWebhookService(
			IOrderServices orderServices,
			IPaymentServices paymentServices,
			IUnitOfWork unitOfWork,
			ILogger<PaymentWebhookService> logger,
			IConfiguration configuration)
		{
			_orderServices = orderServices;
			_paymentServices = paymentServices;
			_unitOfWork = unitOfWork;
			_logger = logger;
			_configuration = configuration;
		}

		public async Task<bool> HandlePaymobAsync(PaymobWebhookDto dto, string receivedHmac)
		{
			_logger.LogInformation("Received Paymob webhook: {@WebhookDto}", dto);

			if (dto?.Obj == null)
			{
				_logger.LogWarning("Paymob webhook has null Obj payload. Ignored.");
				return false;
			}
			var obj = JObject.FromObject(dto.Obj);

            // validate HMAC
            string? secretKey = _configuration["Security:Paymob:HMAC"];

            if (string.IsNullOrEmpty(secretKey))
			{
				_logger.LogError("Paymob HMAC secret key not configured");
				return false;
			}

			bool isValid = VerifyPaymobHmac(obj, receivedHmac, secretKey);
			if (!isValid)
			{
				_logger.LogError("HMAC validation failed for Paymob webhook TxnId={TxnId}", dto.Obj.Id);
				return false; // reject
			}

			// Start transaction
			using var transaction = await _unitOfWork.BeginTransactionAsync();
			
			try
			{
				var webhookResult = await ProcessWebhookData(dto);
				if (!webhookResult.Success)
				{
					await transaction.RollbackAsync();
					return false;
				}
					await _unitOfWork.CommitAsync();
				await transaction.CommitAsync();
				_logger.LogInformation("Successfully processed Paymob webhook for Order {OrderId}", webhookResult.OrderId);
				return true;
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error while handling Paymob webhook");
				await transaction.RollbackAsync();
				return false;
			}
		}

		private async Task<(bool Success, int OrderId)> ProcessWebhookData(PaymobWebhookDto dto)
		{
			var transaction = dto.Obj;
			var webhook = new PaymentWebhook
			{
				TransactionId = transaction.Id,
				OrderId = 0,
				PaymentMethod = transaction.SourceData?.Type ?? "Unknown",
				Success = transaction.Success,
				Status = transaction.Success ? "Approved" : "Declined",
				AmountCents = transaction.AmountCents,
				Currency = transaction.Currency ?? "EGP",
				SourceSubType = transaction.SourceData?.SubType,
				SourceIssuer = dto.IssuerBank,
				CardLast4 = transaction.SourceData?.PanLast4,
				PaymentProvider = "PayMob",
				ProviderOrderId = transaction.Order?.Id.ToString(),
				RawData = System.Text.Json.JsonSerializer.Serialize(dto),
				HmacVerified = true,
				AuthorizationCode = ExtractAuthorizationCode(transaction),
				ReceiptNumber = ExtractReceiptNumber(transaction),
				Is3DSecure = transaction.SourceData?.Type == "card",
				IsCapture = false,
				IsVoided = false,
				IsRefunded = false,
				IntegrationId = transaction.PaymentKeyClaims?.IntegrationId.ToString(),
				ProfileId = transaction.PaymentKeyClaims?.UserId.ToString(),
				ProcessedAt = DateTime.UtcNow,
				RetryCount = 0,
				WebhookUniqueKey = $"{transaction.Id}_{transaction.Order?.Id}_{transaction.AmountCents}"
			};

			if (transaction.Order != null)
			{
				_logger.LogInformation("Paymob order extra: PaidAmountCents={PaidAmountCents}, PaymentStatus={PaymentStatus}",
					transaction.Order.PaidAmountCents, transaction.Order.PaymentStatus);
				webhook.PaymobOrderId = transaction.Order.Id;
			}

			int localOrderId = await ExtractAndValidateOrderId(transaction);
			if (localOrderId > 0)
			{
				webhook.OrderId = localOrderId;
			}

			// Idempotency check
			bool isExist = await _unitOfWork.Repository<PaymentWebhook>()
				.GetAll()
				.AnyAsync(w => w.WebhookUniqueKey == webhook.WebhookUniqueKey);

			if (isExist)
			{
				_logger.LogWarning("Duplicate webhook detected with key: {WebhookUniqueKey}", webhook.WebhookUniqueKey);
				return (true, localOrderId); // Success but already processed
			}

			await _unitOfWork.Repository<PaymentWebhook>().CreateAsync(webhook);

			if (localOrderId <= 0)
			{
				_logger.LogWarning("Webhook could not be linked to a local order. TxnId: {TxnId}", transaction.Id);
				return (true, 0); // Success but no order to update
			}

			// Update payment and order status
			var updateResult = await UpdatePaymentAndOrderStatus(transaction, localOrderId);
			if (!updateResult.Success)
			{
				return (false, localOrderId);
			}
				await _unitOfWork.CommitAsync();
			webhook.PaymentId = updateResult.PaymentId;
			return (true, localOrderId);
		}

		private async Task<int> ExtractAndValidateOrderId(PaymobTransactionObj transaction)
		{
			if (string.IsNullOrWhiteSpace(transaction.Order?.MerchantOrderId) ||
				!int.TryParse(transaction.Order.MerchantOrderId, out var parsedOrderId))
			{
				return 0;
			}

			// Validate that the order exists in our database
			var orderExists = await _unitOfWork.Repository<Models.Order>()
				.GetAll()
				.AnyAsync(o => o.Id == parsedOrderId);

			if (!orderExists)
			{
				_logger.LogWarning("Order {OrderId} not found in database", parsedOrderId);
				return 0;
			}

			return parsedOrderId;
		}

		private async Task<(bool Success, int? PaymentId)> UpdatePaymentAndOrderStatus(PaymobTransactionObj transaction, int localOrderId)
		{
			PaymentStatus status = PaymentStatus.Failed;
			OrderStatus orderStatus = OrderStatus.PendingPayment;
			
			if (transaction.Pending) 
			{
				status = PaymentStatus.Pending;
			}
			else if (transaction.Success)
			{
				status = PaymentStatus.Completed;
				orderStatus = OrderStatus.Processing;
			}

			var paymentResult = await _paymentServices.UpdatePaymentAfterPaid(
				localOrderId, 
				transaction.Id.ToString(), 
				transaction.PaymentKeyClaims?.OrderId ?? 0, 
				status);

			if (!paymentResult.Success)
			{
				_logger.LogError("Failed to update payment for order {OrderId}", localOrderId);
				return (false, null);
			}

			var orderUpdateResult = await _orderServices.UpdateOrderAfterPaid(localOrderId, orderStatus);
			if (!orderUpdateResult.Success)
			{
				_logger.LogError("Failed to update order status for order {OrderId}", localOrderId);
				return (false, paymentResult.Data);
			}

			return (true, paymentResult.Data);
		}

		private bool VerifyPaymobHmac(JObject obj, string receivedHmac, string secretKey)
		{
			try
			{
				var sb = new StringBuilder();
				foreach (var field in HmacFieldsOrder)
				{
					string[] parts = field.Split('.');
					JToken? current = obj;
					foreach (var part in parts)
					{
						if (current == null || current.Type == JTokenType.Null)
						{
							current = null;
							break;
						}
						current = current[part];
					}

					if (current == null || current.Type == JTokenType.Null)
					{
						sb.Append("");
					}
					else if (current.Type == JTokenType.Boolean)
					{
						sb.Append(current.Value<bool>() ? "true" : "false");
					}
					else
					{
						sb.Append(current.ToString());
					}
				}

				var dataToHash = sb.ToString();

				using var hmac = new HMACSHA512(Encoding.UTF8.GetBytes(secretKey));
				var hashBytes = hmac.ComputeHash(Encoding.UTF8.GetBytes(dataToHash));
				var calculatedHmac = BitConverter.ToString(hashBytes).Replace("-", "").ToLower();

				return string.Equals(calculatedHmac, receivedHmac, StringComparison.OrdinalIgnoreCase);
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error calculating HMAC for Paymob webhook");
				return false;
			}
		}

		private string? ExtractAuthorizationCode(PaymobTransactionObj obj) =>
			obj.SourceData?.SubType == "card" ? $"AUTH_{obj.Id}" : null;

		private string? ExtractReceiptNumber(PaymobTransactionObj obj) =>
			obj.SourceData?.SubType == "card" ? $"RECEIPT_{obj.Id}" : null;
	}
}
