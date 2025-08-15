using E_Commerce.DtoModels.PaymentDtos;
using E_Commerce.Enums;
using E_Commerce.Models;
using E_Commerce.Services.AdminOpreationServices;
using E_Commerce.Services.EmailServices;
using E_Commerce.Services.PaymentMethodsServices;
using E_Commerce.Services.PaymentProccessor;
using E_Commerce.Services.ProductServices;
using E_Commerce.UOW;
using Hangfire;
using Microsoft.AspNetCore;
using Microsoft.EntityFrameworkCore;
using StackExchange.Redis;
using static Microsoft.EntityFrameworkCore.DbLoggerCategory;

namespace E_Commerce.Services.PaymentServices
{
    public interface IPaymentServices
    {
        Task<Result<PaymentResponseDto>> CreatePaymentMethod(CreatePayment paymentdto, string userid);
		public  Task<Result<int>> UpdatePaymentAfterPaied(int orderid, string TransactionId, PaymentStatus status);

	}

    public class PaymentServices : IPaymentServices
	{
		private readonly IPaymentMethodsServices _paymentMethodsServices;
		private readonly IBackgroundJobClient _backgroundJobClient;
		private readonly IPaymentProcessor _paymentProcessor;
		private readonly IErrorNotificationService _errorNotificationService;
		private readonly IAdminOpreationServices _adminOperationServices;
		private readonly IProductVariantService _productVariantService;
		public readonly IUnitOfWork _unitOfWork;
        public readonly ILogger<PaymentServices> _logger;
		public PaymentServices(
			IProductVariantService productVariantService,
			IPaymentProcessor paymentProcessor,
            IPaymentMethodsServices paymentMethodsServices, IUnitOfWork unitOfWork, ILogger<PaymentServices> logger, IBackgroundJobClient backgroundJobClient, IErrorNotificationService errorNotificationService, IAdminOpreationServices adminOpreationServices)
		{

			_productVariantService = productVariantService;
			_paymentProcessor = paymentProcessor;
			_paymentMethodsServices = paymentMethodsServices;
			_unitOfWork = unitOfWork;
			_backgroundJobClient = backgroundJobClient;
			_adminOperationServices = adminOpreationServices;
			_errorNotificationService = errorNotificationService;
			_logger = logger;


		}
		public async Task CheckOnPaymentIfNotPaidReturnQuantity(int paymentId, int orderId)
		{
			var payment = await _unitOfWork.Repository<Payment>()
				.GetAll()
				.Where(p => p.Id == paymentId && p.OrderId == orderId)
				.Select(p => new
				{
					p.Status,
					p.CreatedAt,
					Items = p.Order.Items.Select(i => new
					{
						Quantity = i.Quantity,
						ProductId = i.ProductId,
						VariantId = i.ProductVariantId
					}).ToList()
				})
				.FirstOrDefaultAsync();

			if (payment is null)
			{
				_logger.LogError($"Can't find Payment with this id: {paymentId}");
				return;
			}

			if (payment.Status == PaymentStatus.Completed)
				return;

		


			if (DateTime.UtcNow - payment.CreatedAt < TimeSpan.FromHours(24))
				return;

			foreach (var i in payment.Items)
			{
				_backgroundJobClient.Enqueue(()=> _productVariantService.AddQuntityAfterRestoreOrder(i.VariantId, i.Quantity));
			}

		}

		public async Task<Result< int>> UpdatePaymentAfterPaied(int orderid,string TransactionId, PaymentStatus status)
		{
			var latestPayment = await _unitOfWork.Repository<Payment>()
			.GetAll()
			.Where(p => p.OrderId == orderid)
			.OrderByDescending(p => p.Id).FirstOrDefaultAsync();

			if (latestPayment != null)
			{
				latestPayment.Status = status;
				latestPayment.TransactionId = TransactionId;

				_logger.LogInformation("Updated payment {PaymentId} with status {Status} and transaction ID {TransactionId}",
					latestPayment.Id, status, TransactionId);
				var isupdated = _unitOfWork.Repository<Payment>().Update(latestPayment);
				if (!isupdated)
				{
					_logger.LogError("Payment Not Update");
					_backgroundJobClient.Enqueue(()=> _errorNotificationService.SendErrorNotificationAsync("Error While Update payment",null));
					return Result<int>.Fail("Error While Update payment");
				}
				return Result<int>.Ok(orderid);
			}
			return  Result<int>.Fail("Can't find payment");

		}
        public async Task<Result<PaymentResponseDto>> CreatePaymentMethod(CreatePayment paymentdto, string userid)
		{
			_logger.LogInformation("Starting CreatePaymentMethod for user {UserId}", userid);

			if (paymentdto == null)
			{
				_logger.LogWarning("CreatePaymentMethod called with null DTO by user {UserId}", userid);
				return Result<PaymentResponseDto>.Fail("Payment data is required.");
			}

			var orderExists = await _unitOfWork.Repository<Models.Order>().IsExsistAsync(paymentdto.OrderId);
			if (!orderExists)
			{
				_logger.LogWarning("Order not found: {OrderId}", paymentdto.OrderId);
				return Result<PaymentResponseDto>.Fail("Invalid Order ID.");
			}

		

			try
			{
				var methodResult = await _paymentMethodsServices.GetPaymentMethodIdByEnum(paymentdto.PaymentMethod);

				if (!methodResult.Success || methodResult.Data == null)
				{
					_logger.LogWarning("Invalid payment method: {Method}", paymentdto.PaymentMethod);
					
					return Result<PaymentResponseDto>.Fail("Invalid Payment Method.");
				}

				var paymentMethodId = methodResult.Data.Value;

			// Retrieve provider id from payment method
			var paymentMethodEntity = await _unitOfWork.Repository<PaymentMethod>().GetByIdAsync(paymentMethodId);
			if (paymentMethodEntity == null)
			{
				_logger.LogWarning("Payment method entity not found for ID {Id}", paymentMethodId);
			
				return Result<PaymentResponseDto>.Fail("Payment method not found.");
			}

				var payment = new Payment
				{
					Amount = paymentdto.Amount,
					Status = PaymentStatus.Pending,
					PaymentMethodId = paymentMethodId,
					PaymentProviderId = paymentMethodEntity.PaymentProviderId,
					OrderId = paymentdto.OrderId,
					CustomerId = paymentdto.CustomerId
				};

				var response = new PaymentResponseDto
				{
					IsRedirectRequired = false,
					RedirectUrl = null,
					Message = "Payment is Cash On Delivery."
				};

				if (paymentdto.PaymentMethod != PaymentMethodEnums.CashOnDelivery)
				{
					var onlinePaymentResult = await _paymentProcessor.GetPaymentLinkAsync(new CreatePayment
					{
						Amount = paymentdto.Amount,
						Currency = paymentdto.Currency,
						CustomerId = paymentdto.CustomerId,
						Notes = paymentdto.Notes,
						OrderId = paymentdto.OrderId,
						PaymentMethod = paymentdto.PaymentMethod,
						AddressId= paymentdto.AddressId,WalletPhoneNumber=paymentdto.WalletPhoneNumber
					});

					if (!onlinePaymentResult.Success || string.IsNullOrEmpty(onlinePaymentResult.Data))
					{
						_logger.LogWarning("Failed to generate online payment link.");
						return Result<PaymentResponseDto>.Fail(onlinePaymentResult.Message);
					}

					response = new PaymentResponseDto
					{
						IsRedirectRequired = true,
						RedirectUrl = onlinePaymentResult.Data,
						Message = "Please proceed to payment using the provided link."
					};
				}

				var createdPayment = await _unitOfWork.Repository<Payment>().CreateAsync(payment);
				await _unitOfWork.CommitAsync();

				_logger.LogInformation("Payment created successfully with ID {Id}", createdPayment.Id);

				var adminLogResult = await _adminOperationServices.AddAdminOpreationAsync(
					"Create Payment", Opreations.AddOpreation, userid, createdPayment.Id);

				if (!adminLogResult.Success)
				{
					_logger.LogWarning("Failed to log admin operation for Payment ID {Id}", createdPayment.Id);
				
					return Result<PaymentResponseDto>.Fail("Failed to log admin operation.");
				}

				await _unitOfWork.CommitAsync();
				_backgroundJobClient.Schedule(
				() => CheckOnPaymentIfNotPaidReturnQuantity(createdPayment.Id, paymentdto.OrderId),
					TimeSpan.FromHours(24)
						);



				return Result<PaymentResponseDto>.Ok(response);
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error occurred while creating payment for user {UserId}", userid);
				

				_backgroundJobClient.Enqueue(() =>
					_errorNotificationService.SendErrorNotificationAsync("Error in CreatePaymentMethod", ex.Message));

				return Result<PaymentResponseDto>.Fail("Internal server error.", 500);
			}
        }
    }
}
