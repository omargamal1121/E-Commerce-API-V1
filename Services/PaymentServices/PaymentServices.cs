using E_Commerce.DtoModels.OrderDtos;
using E_Commerce.DtoModels.PaymentDtos;
using E_Commerce.Enums;
using E_Commerce.Interfaces;
using E_Commerce.Models;
using E_Commerce.Services.AdminOpreationServices;
using E_Commerce.Services.EmailServices;
using E_Commerce.Services.Order;
using E_Commerce.Services.PaymentMethodsServices;
using E_Commerce.Services.PaymentProccessor;
using E_Commerce.Services.ProductServices;
using E_Commerce.Services.UserOpreationServices;
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
        Task<Result<PaymentResponseDto>> CreatePaymentMethod(string ordernumber,CreatePaymentOfCustomer paymentdto, string userid);
		public  Task<Result<int>> UpdatePaymentAfterPaid(int orderid, string TransactionId,long orderidofpaymob, PaymentStatus status);

	}

    public class PaymentServices : IPaymentServices
	{
		private readonly IPaymentMethodsServices _paymentMethodsServices;
		private	readonly IOrderServices _orderservices;
		private readonly IBackgroundJobClient _backgroundJobClient;
		private readonly IPaymentProcessor _paymentProcessor;
		private readonly IErrorNotificationService _errorNotificationService;
		private readonly IAdminOpreationServices _adminOperationServices;
		private readonly IUserOpreationServices _userOpreationServices ;
		private readonly IProductVariantService _productVariantService;
		public readonly IUnitOfWork _unitOfWork;
        public readonly ILogger<PaymentServices> _logger;
		public PaymentServices(
			IOrderServices orderservices,
			IUserOpreationServices userOpreationServices,
			IProductVariantService productVariantService,
			IPaymentProcessor paymentProcessor,
            IPaymentMethodsServices paymentMethodsServices, IUnitOfWork unitOfWork, ILogger<PaymentServices> logger, IBackgroundJobClient backgroundJobClient, IErrorNotificationService errorNotificationService, IAdminOpreationServices adminOpreationServices)
		{
			_userOpreationServices = userOpreationServices;
			_orderservices = orderservices;
			_productVariantService = productVariantService;
			_paymentProcessor = paymentProcessor;
			_paymentMethodsServices = paymentMethodsServices;
			_unitOfWork = unitOfWork;
			_backgroundJobClient = backgroundJobClient;
			_adminOperationServices = adminOpreationServices;
			_errorNotificationService = errorNotificationService;
			_logger = logger;


		}
		
		public async Task<Result<int>> UpdatePaymentAfterPaid(int orderId, string transactionId, long providerOrderId, PaymentStatus status)
		{
			var latestPayment = await _unitOfWork.Repository<Payment>()
				.GetAll()
				.Where(p => p.OrderId == orderId && p.ProviderOrderId == providerOrderId)
				.OrderByDescending(p => p.Id)
				.FirstOrDefaultAsync();

			if (latestPayment == null)
				return Result<int>.Fail("Can't find payment");

			latestPayment.Status = status;
			latestPayment.TransactionId = transactionId;

			_logger.LogInformation("Updating payment {PaymentId} with status {Status} and transaction ID {TransactionId}",
				latestPayment.Id, status, transactionId);

			var updated = _unitOfWork.Repository<Payment>().Update(latestPayment);
			if (!updated)
			{
				_logger.LogError("Payment update failed for Payment ID {PaymentId}", latestPayment.Id);
				_backgroundJobClient.Enqueue(() =>
					_errorNotificationService.SendErrorNotificationAsync("Error while updating payment", null)
				);
				return Result<int>.Fail("Error while updating payment");
			}

			await _unitOfWork.CommitAsync();

			return Result<int>.Ok(latestPayment.Id);
		}

		public async Task<Result<PaymentResponseDto>> CreatePaymentMethod(string ordernumber,CreatePaymentOfCustomer paymentdto, string userid)
		{
			_logger.LogInformation("Starting CreatePaymentMethod for user {UserId}", userid);

			if (paymentdto == null)
				return Result<PaymentResponseDto>.Fail("Payment data is required.");

			var order = await _unitOfWork.Order.GetOrderByNumberAsync(ordernumber);
			if (order == null || order.CustomerId != userid)
			{
				_logger.LogWarning("Order not found or does not belong to user {UserId}", userid);
				return Result<PaymentResponseDto>.Fail("Invalid Order ID or unauthorized access.");
			}

			if (order.Status!=OrderStatus.PendingPayment)
			{
				_logger.LogWarning("Order {OrderId} is not eligible for payment. Current status: {Status}", order.Id, order.Status);
				return Result<PaymentResponseDto>.Fail("This order cannot be paid due to its current status.", 400);
			}

			try
			{
				var methodResult = await _paymentMethodsServices.GetPaymentMethodIdByEnum(paymentdto.PaymentMethod);
				if (!methodResult.Success || methodResult.Data == null)
					return Result<PaymentResponseDto>.Fail("Invalid Payment Method.");

				var paymentMethodId = methodResult.Data.Value;

				var paymentMethodEntity = await _unitOfWork.Repository<PaymentMethod>().GetByIdAsync(paymentMethodId);
				if (paymentMethodEntity == null)
					return Result<PaymentResponseDto>.Fail("Payment method not found.");

				var payment = new Payment
				{
					Amount = order.Total,
					Status = PaymentStatus.Pending,
					PaymentMethodId = paymentMethodId,
					PaymentProviderId = paymentMethodEntity.PaymentProviderId,
					OrderId = order.Id,
					CustomerId = userid, 
				};

				var response = new PaymentResponseDto
				{
					IsRedirectRequired = false,
					RedirectUrl = null,
					Message = "Cash on Delivery selected. No redirect required."
				};

				if (paymentdto.PaymentMethod != PaymentMethodEnums.CashOnDelivery)
				{
					int timeremaining = (int)(order.CreatedAt.Value.AddHours(2) - DateTime.UtcNow).TotalSeconds;
					if (timeremaining <= 0)
					{
						return Result<PaymentResponseDto>.Fail("This order has expired and cannot be paid.", 400);
					}
					CreatePayment createPayment = new CreatePayment
					{
						Amount = payment.Amount,
						Currency = paymentdto.Currency,
						CustomerId = userid,
						Notes = paymentdto.Notes,
						Ordernumber = ordernumber,
						PaymentMethod = paymentdto.PaymentMethod,
						WalletPhoneNumber = paymentdto.WalletPhoneNumber,
						AddressId= order.CustomerAddressId
					};
					var onlinePaymentResult = await _paymentProcessor.GetPaymentLinkAsync(createPayment, timeremaining);

					if (!onlinePaymentResult.Success || onlinePaymentResult.Data == null)
						return Result<PaymentResponseDto>.Fail(onlinePaymentResult.Message);

					response = new PaymentResponseDto
					{
						IsRedirectRequired = true,
						RedirectUrl = onlinePaymentResult.Data.PaymentUrl,
						Message = "Redirect to the provided link to complete payment."
					};
					payment.ProviderOrderId = onlinePaymentResult.Data.PaymobOrderId;
				}
				else
					await _orderservices.ProcessOrderAsync(order.Id, userid);




				var createdPayment = await _unitOfWork.Repository<Payment>().CreateAsync(payment);
				if (createdPayment == null)
				{
					_logger.LogError("Payment creation failed for user {UserId}", userid);
					return Result<PaymentResponseDto>.Fail("Error while creating payment.");
				}
			

				var logResult = await _userOpreationServices.AddUserOpreationAsync(
					$"Created payment for order {order.Id}",
					Opreations.AddOpreation,
					userid,
					createdPayment.Id
				);

				if (!logResult.Success)
				{
					_logger.LogWarning("Failed to log user operation for Payment ID {Id}", createdPayment.Id);
					return Result<PaymentResponseDto>.Fail("Failed to log user operation.");
				}

				await _unitOfWork.CommitAsync();

				_logger.LogInformation("Payment created successfully with ID {Id}", createdPayment.Id);
				if (paymentdto.PaymentMethod != PaymentMethodEnums.CashOnDelivery)
				{
					_backgroundJobClient.Schedule(() =>
						CheckAndUpdatePaymentStatusAsync(createdPayment.Id),
						TimeSpan.FromHours(1));
				}



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
		public async Task CheckAndUpdatePaymentStatusAsync(int paymentId)
		{
			var payment = await _unitOfWork.Repository<Payment>().GetByIdAsync(paymentId);
			if (payment == null || payment.Status != PaymentStatus.Pending)
				return; // Nothing to do.

			var statusResponse = await _paymentProcessor.GetPaymentStatusAsync(payment.ProviderOrderId);

			if (!statusResponse.Success || statusResponse.Data == null)
			{
				_logger.LogWarning("Failed to fetch status for payment {PaymentId}", paymentId);
				return;
			}

			var remoteStatus = statusResponse.Data.Status; // "Paid", "Pending", "Unpaid"

			PaymentStatus newStatus = remoteStatus == "Paid"
				? PaymentStatus.Completed
				: remoteStatus == "Unpaid"
					? PaymentStatus.Failed
					: PaymentStatus.Pending;

			if (newStatus != payment.Status)
			{
				payment.Status = newStatus;
				await _unitOfWork.CommitAsync();

				var orderStatus = newStatus == PaymentStatus.Completed
					? OrderStatus.Processing
					: OrderStatus.PaymentExpired;

				await _orderservices.UpdateOrderAfterPaid(payment.OrderId, orderStatus);

				_logger.LogInformation("Payment {PaymentId} updated to {Status}", paymentId, newStatus);
			}
		}

	}
}
