using E_Commerce.DtoModels.OrderDtos;
using E_Commerce.DtoModels.PaymentDtos;
using E_Commerce.Enums;
using E_Commerce.Interfaces;
using E_Commerce.Models;
using E_Commerce.Services.AdminOperationServices;
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
        Task<Result<PaymentResponseDto>> CreatePaymentMethod(string ordernumber, CreatePaymentOfCustomer paymentdto, string userid);
        Task<Result<int>> UpdatePaymentAfterPaid(int orderid, string TransactionId, long orderidofpaymob, PaymentStatus status);
    }

    public class PaymentServices : IPaymentServices
    {
        private readonly IPaymentMethodsServices _paymentMethodsServices;
        private readonly IOrderServices _orderservices;
        private readonly IBackgroundJobClient _backgroundJobClient;
        private readonly IPaymentProcessor _paymentProcessor;
        private readonly IErrorNotificationService _errorNotificationService;
        private readonly IAdminOpreationServices _adminOperationServices;
        private readonly IUserOpreationServices _userOpreationServices;
        private readonly IProductVariantService _productVariantService;
        private readonly IUnitOfWork _unitOfWork;
        private readonly ILogger<PaymentServices> _logger;

        public PaymentServices(
            IOrderServices orderservices,
            IUserOpreationServices userOpreationServices,
            IProductVariantService productVariantService,
            IPaymentProcessor paymentProcessor,
            IPaymentMethodsServices paymentMethodsServices, 
            IUnitOfWork unitOfWork, 
            ILogger<PaymentServices> logger, 
            IBackgroundJobClient backgroundJobClient, 
            IErrorNotificationService errorNotificationService, 
            IAdminOpreationServices adminOpreationServices)
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
            _logger.LogInformation("Starting UpdatePaymentAfterPaid for order {OrderId} with status {Status}", orderId, status);

            using var transaction = await _unitOfWork.BeginTransactionAsync();
            
            try
            {
                if (string.IsNullOrEmpty(transactionId))
                {
                    _logger.LogWarning("Transaction ID is null or empty for order {OrderId}", orderId);
                    await transaction.RollbackAsync();
                    return Result<int>.Fail("Transaction ID is required");
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
                    return Result<int>.Fail("Payment not found");
                }

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
                    return Result<int>.Fail("Failed to update payment");
                }
					await _unitOfWork.CommitAsync();
                await transaction.CommitAsync();
                _logger.LogInformation("Payment {PaymentId} updated successfully", latestPayment.Id);
                return Result<int>.Ok(latestPayment.Id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating payment for order {OrderId}", orderId);
                await transaction.RollbackAsync();
                
                _backgroundJobClient.Enqueue(() =>
                    _errorNotificationService.SendErrorNotificationAsync("Error in UpdatePaymentAfterPaid", ex.Message));
                
                return Result<int>.Fail("Error while updating payment");
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
                // Validate order with transaction isolation
                var order = await _unitOfWork.Order.GetOrderByNumberAsync(ordernumber);
                if (order == null)
                {
                    _logger.LogWarning("Order not found with number {OrderNumber}", ordernumber);
                    await transaction.RollbackAsync();
                    return Result<PaymentResponseDto>.Fail("Order not found.");
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

                // Check if payment already exists for this order
                var existingPayment = await _unitOfWork.Repository<Payment>()
                    .GetAll()
                    .Where(p => p.OrderId == order.Id && p.Status == PaymentStatus.Pending)
                    .FirstOrDefaultAsync();

                if (existingPayment != null)
                {
                    _logger.LogWarning("Payment already exists for order {OrderId}", order.Id);
                    await transaction.RollbackAsync();
                    return Result<PaymentResponseDto>.Fail("Payment already exists for this order.", 400);
                }

                // Validate payment method
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

                // Handle online payment vs cash on delivery
                if (paymentdto.PaymentMethod != PaymentMethodEnums.CashOnDelivery)
                {
                    var onlinePaymentResult = await ProcessOnlinePayment(paymentdto, order, payment);
                    if (!onlinePaymentResult.Success)
                    {
                        await transaction.RollbackAsync();
                        return Result<PaymentResponseDto>.Fail(onlinePaymentResult.Message);
                    }
                    response = onlinePaymentResult.Data;
                }
                else
                {
                    var processResult = await _orderservices.ProcessOrderAsync(order.Id, userid);
                    if (!processResult.Success)
                    {
                        _logger.LogError("Failed to process order {OrderId} for cash on delivery", order.Id);
                        await transaction.RollbackAsync();
                        return Result<PaymentResponseDto>.Fail("Failed to process order for cash on delivery.");
                    }
                }

                // Create payment record
                var createdPayment = await _unitOfWork.Repository<Payment>().CreateAsync(payment);
                if (createdPayment == null)
                {
                    _logger.LogError("Payment creation failed for user {UserId}", userid);
                    await transaction.RollbackAsync();
                    return Result<PaymentResponseDto>.Fail("Error while creating payment.");
                }

                // Log user operation
                var logResult = await _userOpreationServices.AddUserOpreationAsync(
                    $"Created payment for order {order.Id}",
                    Opreations.AddOpreation,
                    userid,
                    createdPayment.Id
                );

                if (!logResult.Success)
                {
                    _logger.LogWarning("Failed to log user operation for Payment ID {Id}", createdPayment.Id);
                    // Don't fail the entire operation for logging failure
                }
					await _unitOfWork.CommitAsync();
                await transaction.CommitAsync();

                _logger.LogInformation("Payment created successfully with ID {Id}", createdPayment.Id);
                
                // Schedule status check for online payments
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

                await transaction.RollbackAsync();

                _backgroundJobClient.Enqueue(() =>
                    _errorNotificationService.SendErrorNotificationAsync("Error in CreatePaymentMethod", ex.Message));

                return Result<PaymentResponseDto>.Fail("Internal server error.", 500);
            }
        }

        private async Task<Result<PaymentResponseDto>> ProcessOnlinePayment(CreatePaymentOfCustomer paymentdto, Models.Order order, Payment payment)
        {
            int timeremaining = (int)(order.CreatedAt.Value.AddHours(2) - DateTime.UtcNow).TotalSeconds;
            if (timeremaining <= 0)
            {
                _logger.LogWarning("Order {OrderId} has expired and cannot be paid", order.Id);
                return Result<PaymentResponseDto>.Fail("This order has expired and cannot be paid.", 400);
            }

            CreatePayment createPayment = new CreatePayment
            {
                Amount = payment.Amount,
                Currency = paymentdto.Currency,
                CustomerId = payment.CustomerId, // Use the customer ID from the payment entity
                Notes = paymentdto.Notes,
                Ordernumber = order.OrderNumber,
                PaymentMethod = paymentdto.PaymentMethod,
                WalletPhoneNumber = paymentdto.WalletPhoneNumber,
                AddressId = order.CustomerAddressId
            };

            var onlinePaymentResult = await _paymentProcessor.GetPaymentLinkAsync(createPayment, timeremaining);

            if (!onlinePaymentResult.Success || onlinePaymentResult.Data == null)
            {
                _logger.LogError("Failed to get payment link for order {OrderId}: {Error}", order.Id, onlinePaymentResult.Message);
                return Result<PaymentResponseDto>.Fail(onlinePaymentResult.Message);
            }

            payment.ProviderOrderId = onlinePaymentResult.Data.PaymobOrderId;

            return Result<PaymentResponseDto>.Ok(new PaymentResponseDto
            {
                IsRedirectRequired = true,
                RedirectUrl = onlinePaymentResult.Data.PaymentUrl,
                Message = "Redirect to the provided link to complete payment."
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
                    await transaction.RollbackAsync();
                    return;
                }

                if (payment.Status != PaymentStatus.Pending)
                {
                    _logger.LogInformation("Payment {PaymentId} is not pending (current status: {Status})", paymentId, payment.Status);
                    await transaction.RollbackAsync();
                    return;
                }

                var statusResponse = await _paymentProcessor.GetPaymentStatusAsync(payment.ProviderOrderId);

                if (!statusResponse.Success || statusResponse.Data == null)
                {
                    _logger.LogWarning("Failed to fetch status for payment {PaymentId}: {Error}", paymentId, statusResponse.Message);
                    await transaction.RollbackAsync();
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
                    ? OrderStatus.Processing
                    : OrderStatus.PaymentExpired;

                var orderUpdateResult = await _orderservices.UpdateOrderAfterPaid(payment.OrderId, orderStatus);
                if (!orderUpdateResult.Success)
                {
                    _logger.LogError("Failed to update order status for payment {PaymentId}", paymentId);
                    await transaction.RollbackAsync();
                    return;
                }

                await transaction.CommitAsync();
                _logger.LogInformation("Payment {PaymentId} updated to {Status}", paymentId, newStatus);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in CheckAndUpdatePaymentStatusAsync for payment {PaymentId}", paymentId);
                await transaction.RollbackAsync();
                
                // Schedule retry for failed background job
                _backgroundJobClient.Schedule(() =>
                    CheckAndUpdatePaymentStatusAsync(paymentId),
                    TimeSpan.FromMinutes(30));
            }
        }
    }
}
