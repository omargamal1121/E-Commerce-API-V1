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
		Task HandlePaymobAsync(PaymobWebhookDto dto, string receivedHmac);
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

		public async Task HandlePaymobAsync(PaymobWebhookDto dto, string receivedHmac)
		{
			_logger.LogInformation("Received Paymob webhook: {@WebhookDto}", dto);

			if (dto?.Obj == null)
			{
				_logger.LogWarning("Paymob webhook has null Obj payload. Ignored.");
				return;
			}

			// convert to JObject for flexible parsing
			var obj = JObject.FromObject(dto.Obj);

			// validate HMAC
			string secretKey = _configuration["Paymob:HMAC"];
			bool isValid = VerifyPaymobHmac(obj, receivedHmac, secretKey);
			if (!isValid)
			{
				_logger.LogError("HMAC validation failed for Paymob webhook TxnId={TxnId}", dto.Obj.Id);
				return; // reject
			}

			try
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

				int localOrderId = 0;
				if (!string.IsNullOrWhiteSpace(transaction.Order?.MerchantOrderId) &&
					int.TryParse(transaction.Order.MerchantOrderId, out var parsed))
				{
					localOrderId = parsed;
				}

				if (localOrderId > 0)
					webhook.OrderId = localOrderId;

				// Idempotency check
				bool isExist = await _unitOfWork.Repository<PaymentWebhook>()
					.GetAll()
					.AnyAsync(w => w.WebhookUniqueKey == webhook.WebhookUniqueKey);

				if (isExist)
				{
					_logger.LogWarning("Duplicate webhook detected with key: {WebhookUniqueKey}", webhook.WebhookUniqueKey);
					return;
				}
				await _unitOfWork.Repository<PaymentWebhook>().CreateAsync(webhook);

				if (localOrderId <= 0)
				{
					_logger.LogWarning("Webhook could not be linked to a local order. TxnId: {TxnId}", transaction.Id);
					await _unitOfWork.CommitAsync();
					return;
				}

				PaymentStatus status = PaymentStatus.Failed;
				OrderStatus orderStatus = OrderStatus.PendingPayment;
				if (transaction.Pending) status = PaymentStatus.Pending;
				else if (transaction.Success)
				{
					status = PaymentStatus.Completed;
					orderStatus = OrderStatus.Processing;
				}

				var paymentResult = await _paymentServices.UpdatePaymentAfterPaid(
					localOrderId, transaction.Id.ToString(), transaction.PaymentKeyClaims.OrderId, status);

				if (!paymentResult.Success)
				{
					_logger.LogError("Failed to update payment for order {OrderId}", localOrderId);
					return;
				}
				webhook.PaymentId = paymentResult.Data;

				await _orderServices.UpdateOrderAfterPaid(localOrderId, orderStatus);

				await _unitOfWork.CommitAsync();

				_logger.LogInformation("Successfully processed Paymob webhook for Order {OrderId}", localOrderId);
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error while handling Paymob webhook");
			}
		}

		private bool VerifyPaymobHmac(JObject obj, string receivedHmac, string secretKey)
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

		private string? ExtractAuthorizationCode(PaymobTransactionObj obj) =>
			obj.SourceData?.SubType == "card" ? $"AUTH_{obj.Id}" : null;

		private string? ExtractReceiptNumber(PaymobTransactionObj obj) =>
			obj.SourceData?.SubType == "card" ? $"RECEIPT_{obj.Id}" : null;
	}
}
