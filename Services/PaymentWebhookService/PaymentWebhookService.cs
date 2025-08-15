using E_Commerce.DtoModels.PaymentDtos;
using E_Commerce.Enums;
using E_Commerce.Models;
using E_Commerce.UOW;
using Microsoft.EntityFrameworkCore;
using E_Commerce.Services.PaymentServices;
using E_Commerce.Interfaces;

namespace E_Commerce.Services.PaymentWebhookService
{
	public interface IPaymentWebhookService
	{
		Task HandlePaymobAsync(PaymobWebhookDto dto);
	}

	public class PaymentWebhookService : IPaymentWebhookService
	{
		private readonly IUnitOfWork _unitOfWork;
		private readonly ILogger<PaymentWebhookService> _logger;
		private readonly IPaymentServices _paymentServices;
		private readonly IOrderServices _orderServices;

		public PaymentWebhookService(
			IOrderServices orderServices,
			IPaymentServices paymentServices,
			IUnitOfWork unitOfWork,
			ILogger<PaymentWebhookService> logger)
		{
			_orderServices = orderServices;
			_paymentServices = paymentServices;
			_unitOfWork = unitOfWork;
			_logger = logger;
		}

		public async Task HandlePaymobAsync(PaymobWebhookDto dto)
		{
			_logger.LogInformation("Received Paymob webhook: {@WebhookDto}", dto);

			if (dto?.Obj == null)
			{
				_logger.LogWarning("Paymob webhook has null Obj payload. Ignored.");
				return;
			}

			try
			{
				var obj = dto.Obj;

				// Prepare webhook entity
				var webhook = new PaymentWebhook
				{
					TransactionId = obj.Id,
					OrderId = 0,
					PaymentMethod = obj.SourceData?.Type ?? "Unknown",
					Success = obj.Success,
					Status = obj.Success ? "Approved" : "Declined",
					AmountCents = obj.AmountCents,
					Currency = obj.Currency ?? "EGP",
					SourceSubType = obj.SourceData?.SubType,
					SourceIssuer = dto.IssuerBank,
					CardLast4 = obj.SourceData?.PanLast4,
					PaymentProvider = "PayMob",
					ProviderOrderId = obj.Order?.Id.ToString(),
					RawData = System.Text.Json.JsonSerializer.Serialize(dto),
					HmacVerified = false,
					AuthorizationCode = ExtractAuthorizationCode(obj),
					ReceiptNumber = ExtractReceiptNumber(obj),
					Is3DSecure = obj.SourceData?.Type == "card",
					IsCapture = false,
					IsVoided = false,
					IsRefunded = false,
					IntegrationId = obj.PaymentKeyClaims?.IntegrationId.ToString(),
					ProfileId = obj.PaymentKeyClaims?.UserId.ToString(),
					ProcessedAt = DateTime.UtcNow,
					RetryCount = 0,
					WebhookUniqueKey = $"{obj.Id}_{obj.Order?.Id}_{obj.AmountCents}"
				};

				if (obj.Order != null)
				{
					_logger.LogInformation("Paymob order extra: PaidAmountCents={PaidAmountCents}, PaymentStatus={PaymentStatus}",
						obj.Order.PaidAmountCents, obj.Order.PaymentStatus);
					webhook.PaymobOrderId = obj.Order.Id;
				}

				// Try to get local order ID
				int localOrderId = 0;
				if (!string.IsNullOrWhiteSpace(obj.Order?.MerchantOrderId) &&
					int.TryParse(obj.Order.MerchantOrderId, out var parsed))
				{
					localOrderId = parsed;
				}
				else if (obj.PaymentKeyClaims != null)
				{
					var amount = obj.AmountCents / 100m;
					var payment = await _unitOfWork.Repository<Payment>()
						.GetAll()
						.Where(p => p.Amount == amount)
						.OrderByDescending(p => p.Id)
						.FirstOrDefaultAsync();

					if (payment != null)
						localOrderId = payment.OrderId;
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

				// Save webhook
				await _unitOfWork.Repository<PaymentWebhook>().CreateAsync(webhook);

				if (localOrderId <= 0)
				{
					_logger.LogWarning("Webhook could not be linked to a local order. TxnId: {TxnId}", obj.Id);
					await _unitOfWork.CommitAsync(); 
					return;
				}

				PaymentStatus status = PaymentStatus.Failed;
				if (obj.Pending) status = PaymentStatus.Pending;
				else if (obj.Success) status = PaymentStatus.Completed;

				var paymentResult = await _paymentServices.UpdatePaymentAfterPaied(localOrderId, obj.Id.ToString(), status);
				if (!paymentResult.Success)
				{
					_logger.LogError("Failed to update payment for order {OrderId}", localOrderId);
					return;
				}
				webhook.PaymentId = paymentResult.Data;

		
				await _orderServices.UpdateOrderAfterPaid(localOrderId, OrderStatus.Processing);

				await _unitOfWork.CommitAsync();

				_logger.LogInformation("Successfully processed Paymob webhook for Order {OrderId}", localOrderId);
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error while handling Paymob webhook");
			}
		}

		private string? ExtractAuthorizationCode(PaymobTransactionObj obj) =>
			obj.SourceData?.SubType == "card" ? $"AUTH_{obj.Id}" : null;

		private string? ExtractReceiptNumber(PaymobTransactionObj obj) =>
			obj.SourceData?.SubType == "card" ? $"RECEIPT_{obj.Id}" : null;
	}
}
