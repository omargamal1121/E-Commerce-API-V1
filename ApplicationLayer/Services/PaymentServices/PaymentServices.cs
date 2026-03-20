using ApplicationLayer.DtoModels.PaymentDtos;
using ApplicationLayer.Interfaces;
using ApplicationLayer.Services.CollectionServices;
using ApplicationLayer.Services.EmailServices;
using ApplicationLayer.Services.OrderService;
using ApplicationLayer.Services.PaymentMethodsServices;
using ApplicationLayer.Services.PaymentProccessor;
using ApplicationLayer.Services.ProductServices;
using ApplicationLayer.Services.ProductVariantServices;
using ApplicationLayer.Services.SubCategoryServices;
using ApplicationLayer.Services.UserOpreationServices;
using DomainLayer.Enums;
using DomainLayer.Models;
using Hangfire;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ApplicationLayer.Services.PaymentServices
{
    public interface IPaymentServices
    {
        Task<Result<PaymentResponseDto>> CreatePaymentMethod(string ordernumber, CreatePaymentOfCustomer paymentdto, string userid);
        Task<Result<int>> UpdatePaymentAfterPaid(int orderid, string TransactionId, long orderidofpaymob, PaymentStatus status);
    }

    public class PaymentServices : IPaymentServices
    {
        private readonly IPaymentMethodsServices _paymentMethodsServices;
		private readonly IProductVariantCacheHelper _productVariantCacheHelper;
		private readonly IProductCacheManger _productCacheManger;
		private readonly ICollectionCacheHelper _collectionCacheHelper;
		private readonly ISubCategoryCacheHelper _subCategoryCacheHelper;
		private readonly IOrderServices _orderservices;
        private readonly IBackgroundJobClient _backgroundJobClient;
        private readonly IPaymentProcessor _paymentProcessor;
        private readonly IErrorNotificationService _errorNotificationService;
        private readonly IUserOpreationServices _userOpreationServices;
        private readonly IUnitOfWork _unitOfWork;
        private readonly IOrderCacheHelper _orderCacheHelper;   
		private readonly ILogger<PaymentServices> _logger;

        public PaymentServices(
			  ISubCategoryCacheHelper subCategoryCacheHelper,
			IProductVariantCacheHelper productVariantCacheHelper,
			IProductCacheManger productCacheManger,
			ICollectionCacheHelper collectionCacheHelper,
			IOrderCacheHelper orderCacheHelper,
			IOrderServices orderservices,
            IUserOpreationServices userOpreationServices,
            IPaymentProcessor paymentProcessor,
            IPaymentMethodsServices paymentMethodsServices, 
            IUnitOfWork unitOfWork, 
            ILogger<PaymentServices> logger, 
            IBackgroundJobClient backgroundJobClient, 
            IErrorNotificationService errorNotificationService)
        {
			_subCategoryCacheHelper = subCategoryCacheHelper;
			_productVariantCacheHelper = productVariantCacheHelper;
			_collectionCacheHelper = collectionCacheHelper;
			_productCacheManger = productCacheManger;
			_orderCacheHelper = orderCacheHelper;
			_userOpreationServices = userOpreationServices;
            _orderservices = orderservices;
            _paymentProcessor = paymentProcessor;
            _paymentMethodsServices = paymentMethodsServices;
            _unitOfWork = unitOfWork;
            _backgroundJobClient = backgroundJobClient;
            _errorNotificationService = errorNotificationService;
            _logger = logger;
        }

        public async Task<Result<int>> UpdatePaymentAfterPaid(int orderId, string transactionId, long providerOrderId, PaymentStatus status)
        {
            _logger.LogInformation("Starting UpdatePaymentAfterPaid for order {OrderId} with status {Status}", orderId, status);

            using var transaction = await _unitOfWork.BeginTransactionAsync();
            
            try
            {
                if (string.IsNullOrEmpty(transactionId))
                {
                    _logger.LogWarning("Transaction ID is null or empty for order {OrderId}", orderId);
                    await transaction.RollbackAsync();
                    return Result<int>.Fail("Transaction ID is required", 400,null);
                }

                var latestPayment = await _unitOfWork.Repository<Payment>()
                    .GetAll()
                    .Where(p => p.OrderId == orderId && p.ProviderOrderId == providerOrderId)
                    .OrderByDescending(p => p.Id)
                    .FirstOrDefaultAsync();

                if (latestPayment == null)
                {
                    _logger.LogWarning("Payment not found for order {OrderId} with provider order ID {ProviderOrderId}", orderId, providerOrderId);
                    await transaction.RollbackAsync();
                    return Result<int>.Fail("Payment not found", 404,null);
                }


                await _unitOfWork.Payment.LockPaymentForUpdateAsync(latestPayment.Id);
                if (latestPayment.Status == status)
                {
                    _logger.LogInformation("Payment {PaymentId} already has status {Status}, no update needed", latestPayment.Id, status);
                    await transaction.RollbackAsync();
                    return Result<int>.Ok(latestPayment.Id);
                }

                latestPayment.Status = status;
                latestPayment.TransactionId = transactionId;
                latestPayment.ModifiedAt = DateTime.UtcNow;

                _logger.LogInformation("Updating payment {PaymentId} with status {Status} and transaction ID {TransactionId}",
                    latestPayment.Id, status, transactionId);

                var updated = _unitOfWork.Repository<Payment>().Update(latestPayment);
                if (!updated)
                {
                    _logger.LogError("Payment update failed for Payment ID {PaymentId}", latestPayment.Id);
                    await transaction.RollbackAsync();
                    return Result<int>.Fail("Failed to update payment", 500, null);
                }
				await _unitOfWork.CommitAsync();
                await transaction.CommitAsync();
                _logger.LogInformation("Payment {PaymentId} updated successfully", latestPayment.Id);
				RemoveCacheAndRelated();
				return Result<int>.Ok(latestPayment.Id);
            }
            catch (DbUpdateConcurrencyException e)
            {
                _logger.LogWarning(e, "Concurrency conflict while updating payment for order {OrderId}", orderId);
                await transaction.RollbackAsync();
                return Result<int>.Fail("Payment was modified by another process.", 409, null);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating payment for order {OrderId}", orderId);
                await transaction.RollbackAsync();
                
                _backgroundJobClient.Enqueue(() =>
                    _errorNotificationService.SendErrorNotificationAsync("Error in UpdatePaymentAfterPaid", ex.Message));
                
                return Result<int>.Fail("Error while updating payment", 500, null);
            }
        }

        public async Task<Result<PaymentResponseDto>> CreatePaymentMethod(string ordernumber, CreatePaymentOfCustomer paymentdto, string userid)
        {
            _logger.LogInformation("Starting CreatePaymentMethod for order {OrderNumber} by user {UserId}", ordernumber, userid);

            if (paymentdto == null)
            {
                _logger.LogWarning("CreatePaymentMethod called with null DTO by user {UserId}", userid);
                return Result<PaymentResponseDto>.Fail("Payment data is required.");
            }

            if (string.IsNullOrEmpty(ordernumber))
            {
                _logger.LogWarning("CreatePaymentMethod called with null order number by user {UserId}", userid);
                return Result<PaymentResponseDto>.Fail("Order number is required.");
            }

            using var transaction = await _unitOfWork.BeginTransactionAsync();
            
            try
            {
            
                var order= await _unitOfWork.Order
                   .GetOrderByNumberAsync(ordernumber);
                if (order == null)
                {
                    _logger.LogWarning("Order not found with number {OrderNumber}", ordernumber);
                    await transaction.RollbackAsync();
                    return Result<PaymentResponseDto>.Fail("Order not found.");
                }
                
                await _unitOfWork.Order.LockOrderForUpdateAsync(order.Id);

                var lastpayment = await _unitOfWork.Repository<Payment>().GetAll().Where(p=> p.OrderId == order.Id).OrderBy(p => p.CreatedAt).ThenBy(p => p.Id).LastOrDefaultAsync();

                if (lastpayment is not null &&
      lastpayment.Status == PaymentStatus.Pending &&
      lastpayment?.CreatedAt?. AddMinutes(10) > DateTime.UtcNow)
                {
                    await transaction.RollbackAsync();
                    return Result<PaymentResponseDto>.Fail("There is already a pending payment...");
                }

                if (order.CustomerId != userid)
                {
                    _logger.LogWarning("Order {OrderId} does not belong to user {UserId}", order.Id, userid);
                    await transaction.RollbackAsync();
                    return Result<PaymentResponseDto>.Fail("Unauthorized access to this order.");
                }

                if (order.Status != OrderStatus.PendingPayment)
                {
                    _logger.LogWarning("Order {OrderId} is not eligible for payment. Current status: {Status}", order.Id, order.Status);
                    await transaction.RollbackAsync();
                    return Result<PaymentResponseDto>.Fail("This order cannot be paid due to its current status.", 400);
                }
                if (order?.CreatedAt?.AddHours(5) <= DateTime.UtcNow)
                {
                    order.Status = OrderStatus.PaymentExpired;
                    await _unitOfWork.CommitAsync();
                    await transaction.CommitAsync();
                    return Result<PaymentResponseDto>.Fail("Order expired. Please create a new order.", 400);
                }

                var methodResult = await _paymentMethodsServices.GetPaymentMethodIdByEnum(paymentdto.PaymentMethod);
                if (!methodResult.Success || methodResult.Data == null)
                {
                    _logger.LogWarning("Invalid payment method {PaymentMethod} for user {UserId}", paymentdto.PaymentMethod, userid);
                    await transaction.RollbackAsync();
                    return Result<PaymentResponseDto>.Fail("Invalid Payment Method.");
                }
             
                var paymentMethodId = methodResult.Data.Value;

                var paymentMethodEntity = await _unitOfWork.Repository<PaymentMethod>().GetByIdAsync(paymentMethodId);
                if (paymentMethodEntity == null)
                {
                    _logger.LogWarning("Payment method {PaymentMethodId} not found", paymentMethodId);
                    await transaction.RollbackAsync();
                    return Result<PaymentResponseDto>.Fail("Payment method not found.");
                }

                if (!paymentMethodEntity.IsActive)
                {
                    _logger.LogWarning("Payment method {PaymentMethodId} is not active", paymentMethodId);
                    await transaction.RollbackAsync();
                    return Result<PaymentResponseDto>.Fail("Payment method is not active.");
                }
                if (lastpayment != null &&
    lastpayment.Status == PaymentStatus.Pending &&
    lastpayment?.CreatedAt?.AddMinutes(10) <= DateTime.UtcNow)
                {
                    lastpayment.Status = PaymentStatus.Failed;
                    _unitOfWork.Repository<Payment>().Update(lastpayment);
                   // await _unitOfWork.CommitAsync(); // Removed intermediate commit
                }

                var payment = new Payment
                {
                    Amount = order.Total,
                    Status = PaymentStatus.Pending,
                    PaymentMethodId = paymentMethodId,
                    PaymentProviderId = paymentMethodEntity.PaymentProviderId,
                    
                    OrderId = order.Id,
                    CustomerId = userid,
                    CreatedAt = DateTime.UtcNow
                };

                var response = new PaymentResponseDto
                {
                    IsRedirectRequired = false,
                    RedirectUrl = null,
                    Message = "Cash on Delivery selected. No redirect required."
                };

               
                if (paymentdto.PaymentMethod != PaymentMethodEnums.CashOnDelivery)
                {
                    var onlinePaymentResult = await ProcessOnlinePayment(paymentdto, order, payment,lastpayment is not null? lastpayment.ProviderOrderId:0);
                    if (!onlinePaymentResult.Success)
                    {
                        await transaction.RollbackAsync();
                        return Result<PaymentResponseDto>.Fail(onlinePaymentResult.Message);
                    }
                  
                    response = onlinePaymentResult.Data;
                }
                else
                {
                   _backgroundJobClient.Enqueue(()=> _orderservices.ConfirmOrderAsync(order.Id, userid,true,false,null));
                }
                var createdPayment = await _unitOfWork.Repository<Payment>().CreateAsync(payment);
                // await _unitOfWork.CommitAsync();  // Removed intermediate commit

                if (createdPayment == null)
                {
                    _logger.LogError("Payment creation failed for user {UserId}", userid);
                    await transaction.RollbackAsync();
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
       
                }
				await _unitOfWork.CommitAsync();
                await transaction.CommitAsync();

                _logger.LogInformation("Payment created successfully with ID {Id}", createdPayment.Id);
                
           
                if (paymentdto.PaymentMethod != PaymentMethodEnums.CashOnDelivery)
                {
                    _backgroundJobClient.Schedule(() =>
                        CheckAndUpdatePaymentStatusAsync(createdPayment.Id),
                        TimeSpan.FromMinutes(8));
                }

                return Result<PaymentResponseDto>.Ok(response!);
            }
           
            catch (DbUpdateException e)
            {
                _logger.LogWarning(e,
                    "Unique constraint violation while creating payment. Order: {OrderNumber}, User: {UserId}",
                    ordernumber, userid);

                await transaction.RollbackAsync();

                var inner = e.InnerException;
                var isDuplicate = false;
                if (inner is MySqlConnector.MySqlException mysqlEx1 && mysqlEx1.Number == 1062)
                {
                    isDuplicate = true;
                }
                else if (inner?.GetType().FullName == "MySql.Data.MySqlClient.MySqlException")
                {
                    var numberProp = inner.GetType().GetProperty("Number");
                    if (numberProp != null && numberProp.GetValue(inner) is int num && num == 1062)
                    {
                        isDuplicate = true;
                    }
                }
                else if (inner?.Message?.Contains("Duplicate entry", StringComparison.OrdinalIgnoreCase) == true
                    || inner?.Message?.Contains("IX_Payments_OrderId_Status_PaymentMethodId", StringComparison.OrdinalIgnoreCase) == true)
                {
                    isDuplicate = true;
                }

                if (isDuplicate)
                {
                    return Result<PaymentResponseDto>.Fail(
                        "A payment is already in progress for this order. Please check your payments history.",
                        409);
                }

                return Result<PaymentResponseDto>.Fail("Database update error.", 500);
            }

            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while creating payment for user {UserId}", userid);

                await transaction.RollbackAsync();

                _backgroundJobClient.Enqueue(() =>
                    _errorNotificationService.SendErrorNotificationAsync("Error in CreatePaymentMethod", ex.Message));

                return Result<PaymentResponseDto>.Fail("Internal server error.", 500);
            }
        }

        private async Task<Result<PaymentResponseDto>> ProcessOnlinePayment(CreatePaymentOfCustomer paymentdto, DomainLayer.Models.Order order, Payment payment,long providerorderid)
        {
            var expirationTime = order.CreatedAt?.AddHours(5);
            
            if (expirationTime is null || expirationTime <= DateTime.UtcNow)
            {
                return Result<PaymentResponseDto>.Fail("Order has expired.", 400);
            }

            var remainingTime = expirationTime.Value - DateTime.UtcNow;

            var maxTime = TimeSpan.FromMinutes(40); // Increased max time to be reasonable
            int timeRemainingSeconds =
                (int)Math.Min(remainingTime.TotalSeconds, maxTime.TotalSeconds);


            CreatePayment createPayment = new CreatePayment
            {
                Amount = payment.Amount,
                Currency = paymentdto.Currency,
                CustomerId = payment.CustomerId, 
                Notes = paymentdto.Notes,
                Ordernumber = order.OrderNumber,
                PaymentMethod = paymentdto.PaymentMethod,
                WalletPhoneNumber = paymentdto.WalletPhoneNumber,
                AddressId = order.CustomerAddressId
            };

            var onlinePaymentResult = await _paymentProcessor.GetPaymentLinkAsync(createPayment, timeRemainingSeconds, providerorderid);

            if (!onlinePaymentResult.Success || onlinePaymentResult.Data == null)
            {
                _logger.LogError("Failed to get payment link for order {OrderId}: {Error}", order.Id, onlinePaymentResult.Message);
                return Result<PaymentResponseDto>.Fail(onlinePaymentResult.Message);
            }

            payment.ProviderOrderId = onlinePaymentResult.Data!.PaymobOrderId;

            return Result<PaymentResponseDto>.Ok(new PaymentResponseDto
            {
                IsRedirectRequired = true,
                RedirectUrl = onlinePaymentResult.Data.PaymentUrl,
                Message = "Redirect to the provided link to complete payment.",
                ProviderOrderId = onlinePaymentResult.Data.PaymobOrderId
            });
        }

        public async Task CheckAndUpdatePaymentStatusAsync(int paymentId)
        {
            _logger.LogInformation("Starting CheckAndUpdatePaymentStatusAsync for payment {PaymentId}", paymentId);

            using var transaction = await _unitOfWork.BeginTransactionAsync();
            
            try
            {
                var payment = await _unitOfWork.Repository<Payment>().GetByIdAsync(paymentId);
                if (payment == null)
                {
                    _logger.LogWarning("Payment {PaymentId} not found", paymentId);
                    return;
                }

                // Lock payment row while we refresh its status
                await _unitOfWork.Payment.LockPaymentForUpdateAsync(paymentId);

                if (payment.Status != PaymentStatus.Pending)
                {
                    _logger.LogInformation("Payment {PaymentId} is not pending (current status: {Status})", paymentId, payment.Status);
                    return;
                }

                var statusResponse = await _paymentProcessor.GetPaymentStatusAsync(payment.ProviderOrderId);

                if (!statusResponse.Success || statusResponse.Data == null)
                {
                    _logger.LogWarning("Failed to fetch status for payment {PaymentId}: {Error}", paymentId, statusResponse.Message);
                    return;
                }

                var remoteStatus = statusResponse.Data.Status;

                PaymentStatus newStatus = remoteStatus switch
                {
                    "Paid" => PaymentStatus.Completed,
                    "Unpaid" => PaymentStatus.Failed,
                    _ => PaymentStatus.Pending
                };

                if (newStatus == payment.Status)
                {
                    _logger.LogInformation("Payment {PaymentId} status unchanged ({Status})", paymentId, newStatus);
                    await transaction.RollbackAsync();
                    return;
                }

                payment.Status = newStatus;
                payment.ModifiedAt = DateTime.UtcNow;
                
                var orderStatus = newStatus == PaymentStatus.Completed
                    ? OrderStatus.Confirmed
                    : OrderStatus.PaymentExpired;

              _backgroundJobClient.Enqueue(()=>  _orderservices.UpdateOrderAfterPaid(payment.OrderId, orderStatus));
          

                await _unitOfWork.CommitAsync();
                await transaction.CommitAsync();
                RemoveCacheAndRelated();
				_logger.LogInformation("Payment {PaymentId} updated to {Status}", paymentId, newStatus);
            }
            catch (DbUpdateConcurrencyException e)
            {
                _logger.LogWarning(e, "Concurrency conflict while updating payment status for {PaymentId}", paymentId);
                await transaction.RollbackAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in CheckAndUpdatePaymentStatusAsync for payment {PaymentId}", paymentId);
                await transaction.RollbackAsync();
                
        
                _backgroundJobClient.Schedule(() =>
                    CheckAndUpdatePaymentStatusAsync(paymentId),
                    TimeSpan.FromMinutes(5));
            }
        }
		private void RemoveCacheAndRelated()
		{
			_orderCacheHelper.ClearOrderCache();
			_productVariantCacheHelper.RemoveProductCachesAsync();
			_productCacheManger.ClearProductCache();
			_collectionCacheHelper.ClearCollectionCache();
			_subCategoryCacheHelper.ClearSubCategoryCache();

		}
	}
}


