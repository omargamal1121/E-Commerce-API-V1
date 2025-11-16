using DomainLayer.Enums;
using ApplicationLayer.DtoModels.OrderDtos;
using ApplicationLayer.Interfaces;
using DomainLayer.Models;
using ApplicationLayer.Services.AdminOperationServices;
using ApplicationLayer.Services.Cache;
using ApplicationLayer.Services.CollectionServices;
using ApplicationLayer.Services.EmailServices;
using ApplicationLayer.Services.ProductServices;
using ApplicationLayer.Services.ProductVariantServices;
using ApplicationLayer.Services.SubCategoryServices;
using ApplicationLayer.Services.UserOpreationServices;
using Hangfire;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;

namespace ApplicationLayer.Services.OrderService
{
    public class OrderCommandService : IOrderCommandService, IDisposable
    {
        private readonly ILogger<OrderCommandService> _logger;
        private readonly IUnitOfWork _unitOfWork;
        private readonly IProductVariantCacheHelper _productVariantCacheHelper;
        private readonly IProductCacheManger _productCacheManger;
        private readonly ICollectionCacheHelper _collectionCacheHelper;
        private readonly ISubCategoryCacheHelper _subCategoryCacheHelper;

		private readonly IUserOpreationServices _userOpreationServices;
        private readonly IOrderRepository _orderRepository;
        private readonly ICartServices _cartServices;
        private readonly IBackgroundJobClient _backgroundJobClient;
        private readonly IErrorNotificationService _errorNotificationService;
        private readonly IAdminOpreationServices _adminOperationServices;
        private readonly IProductCatalogService _productCatalogService;
        private readonly UserManager<Customer> _userManager;
        private readonly IOrderCacheHelper _cacheHelper;
        private readonly IProductVariantCommandService _productVariantCommandService;
        
        // Thread-safe dictionary to store locks for each product variant to prevent race conditions
        private static readonly ConcurrentDictionary<int, SemaphoreSlim> _variantLocks = new();

        public OrderCommandService(
            ISubCategoryCacheHelper subCategoryCacheHelper,
            IProductVariantCacheHelper productVariantCacheHelper,
            IProductCacheManger productCacheManger,
            ICollectionCacheHelper collectionCacheHelper,

			IProductCatalogService productCatalogService,
            IBackgroundJobClient backgroundJobClient,
            IErrorNotificationService errorNotificationService,
            UserManager<Customer> userManager,
            IUserOpreationServices userOpreationServices,
            ILogger<OrderCommandService> logger,
            IUnitOfWork unitOfWork,
            IOrderRepository orderRepository,
            ICartServices cartServices,
            IAdminOpreationServices adminOperationServices,
            IOrderCacheHelper cacheHelper,
            IProductVariantCommandService productVariantCommandService)
        {
            _subCategoryCacheHelper = subCategoryCacheHelper;
            _productVariantCacheHelper = productVariantCacheHelper;
            _collectionCacheHelper = collectionCacheHelper;
            _productCacheManger = productCacheManger;
			_productCatalogService = productCatalogService;
            _backgroundJobClient = backgroundJobClient;
            _errorNotificationService = errorNotificationService;
            _userManager = userManager;
            _userOpreationServices = userOpreationServices;
            _logger = logger;
            _unitOfWork = unitOfWork;
            _orderRepository = orderRepository;
            _cartServices = cartServices;
            _adminOperationServices = adminOperationServices;
            _cacheHelper = cacheHelper;
            _productVariantCommandService = productVariantCommandService;
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Protected dispose method for derived classes
        /// </summary>
        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                foreach (var lockPair in _variantLocks)
                {
                    lockPair.Value?.Dispose();
                }
                _variantLocks.Clear();
            }
        }

        /// <summary>
        /// Finalizer to ensure cleanup
        /// </summary>
        ~OrderCommandService()
        {
            Dispose(false);
        }

        public async Task<Result<OrderAfterCreatedto>> CreateOrderFromCartAsync(string userId, CreateOrderDto orderDto)
        {
            _logger.LogInformation("Creating order from cart for user: {UserId}", userId);

            await using var transaction = await _unitOfWork.BeginTransactionAsync();

            try
            {
                #region Validate User and Cart
                var user = await _userManager.FindByIdAsync(userId);
                if (user == null)
                    return Result<OrderAfterCreatedto>.Fail("UnAuthorized", 401);

                if (!await _unitOfWork.Cart.IsExsistByUserId(userId))
                    return Result<OrderAfterCreatedto>.Fail("Cart is empty", 400);

                if (!await _unitOfWork.CustomerAddress.IsExsistByIdAndUserIdAsync(orderDto.AddressId, userId))
                    return Result<OrderAfterCreatedto>.Fail("Address doesn't exist", 400);

                var cartResult = await _cartServices.GetCartAsync(userId);
                if (!cartResult.Success || cartResult.Data == null || cartResult.Data.IsEmpty)
                    return Result<OrderAfterCreatedto>.Fail("Cart is empty", 400);

                var cart = cartResult.Data;

                if (cart.CheckoutDate == null || cart.CheckoutDate.Value.AddDays(7) < DateTime.UtcNow)
                    return Result<OrderAfterCreatedto>.Fail("Please checkout before creating order", 400);
				#endregion

				#region Check Stock Availability (without reducing yet)
				var variantIds = cart.Items
					.Where(i => i.Product.IsActive && i.DeletedAt == null)
					.Select(i => i.Product.productVariantForCartDto.Id)
					.ToList();

				var variants = await _unitOfWork.Repository<ProductVariant>()
					.GetAll().AsNoTracking().Include(v=>v.Product)
					.Where(v => variantIds.Contains(v.Id))
					.ToListAsync();

				// Validate stock availability first without reducing
				foreach (var item in cart.Items)
				{
					var variant = variants.FirstOrDefault(v => v.Id == item.Product.productVariantForCartDto.Id);
					if (variant == null || variant.Quantity < item.Quantity)
					{
						await transaction.RollbackAsync();
						return Result<OrderAfterCreatedto>.Fail(
							$"Product '{item.Product.Name}' is not available in required quantity. " +
							$"Requested: {item.Quantity}, Available: {(variant?.Quantity ?? 0)}",
							400
						);
					}
				}
				#endregion

				#region Create Order + Items
				var subtotal = cart.Items.Sum(i => i.Quantity * i.UnitPrice);
                var total = subtotal;

                var orderNumber = await _orderRepository.GenerateOrderNumberAsync();

                var order = new DomainLayer.Models.Order
                {
                    CustomerId = userId,
                    OrderNumber = orderNumber,
                    CustomerAddressId = orderDto.AddressId,
                    Status = OrderStatus.PendingPayment,
                    Subtotal = subtotal,
                    Total = total,
                    Notes = orderDto.Notes,
                    CreatedAt = DateTime.UtcNow
                };

                var createdOrder = await _orderRepository.CreateAsync(order);
                if (createdOrder == null)
                {
                    await transaction.RollbackAsync();
                    return Result<OrderAfterCreatedto>.Fail("Failed to create order", 500);
                }
                await _unitOfWork.CommitAsync();

                var orderItems = cart.Items.Select(item => new OrderItem
                {
                    OrderId = createdOrder.Id,
                    ProductId = item.ProductId,
                    ProductVariantId = item.Product.productVariantForCartDto.Id,
                    Quantity = item.Quantity,
                    UnitPrice = item.UnitPrice,
                    TotalPrice = item.Quantity * item.UnitPrice,
                    OrderedAt = DateTime.UtcNow,
                    CreatedAt = DateTime.UtcNow
                }).ToList();

                await _unitOfWork.Repository<OrderItem>().CreateRangeAsync(orderItems.ToArray());
				#endregion

				#region Reduce Stock After Order Creation Succeeds
				foreach (var item in cart.Items)
				{
					var variant = variants.FirstOrDefault(v => v.Id == item.Product.productVariantForCartDto.Id);
					if (variant != null)
					{
						if (variant.Quantity < item.Quantity)
						{
							await transaction.RollbackAsync();
							return Result<OrderAfterCreatedto>.Fail(
								$"Insufficient stock for product '{item.Product.Name}'. " +
								$"Requested: {item.Quantity}, Available: {variant.Quantity}",
								400
							);
						}

						var removeResult = await _productVariantCommandService.RemoveQuntityAfterOrder(variant.Id, item.Quantity);
						if (!removeResult.Success)
						{
							await transaction.RollbackAsync();
							return Result<OrderAfterCreatedto>.Fail(
								$"Failed to reduce stock for product '{item.Product.Name}': No Enough Quntity",
								409
							);
						}
					}
				}
				#endregion


				#region Clear Cart + Commit
				await _unitOfWork.Cart.ClearCartAsync(cart.Id);

                await _unitOfWork.CommitAsync();
                await transaction.CommitAsync();
                #endregion

                #region Log User Operation (best effort)
                var logResult = await _userOpreationServices.AddUserOpreationAsync(
                    $"Created order {orderNumber} from cart",
                    Opreations.AddOpreation,
                    userId,
                    createdOrder.Id
                );

                if (!logResult.Success)
                    _logger.LogWarning("User operation logging failed for order {OrderId}", createdOrder.Id);
                #endregion

                #region Prepare Response
              

            

                RemoveCacheAndRelated();

                var response = new OrderAfterCreatedto { OrderId=createdOrder.Id,
                OrderNumber=order.OrderNumber};

                _backgroundJobClient.Schedule(
                    () => ExpireUnpaidOrderInBackground(createdOrder.Id),
                    TimeSpan.FromHours(2)
                );

                return Result<OrderAfterCreatedto>.Ok(response, "Order created successfully", 201);
                #endregion
            }
            catch (DbUpdateConcurrencyException e)
            {
                await transaction.RollbackAsync();
                _logger.LogWarning(e, "Concurrency conflict while creating order for user {UserId}", userId);
                return Result<OrderAfterCreatedto>.Fail("Order was modified by another process.", 409);
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Exception while creating order for user {UserId}", userId);
                _cacheHelper.NotifyAdminError($"Exception creating order for user {userId}: {ex.Message}", ex.StackTrace);
                return Result<OrderAfterCreatedto>.Fail("An error occurred while creating the order", 500);
            }
        }

		public async Task<Result<bool>> UpdateOrderAfterPaid(int orderId, OrderStatus orderStatus)
		{
			_logger.LogInformation("Execute {Method}_id:{OrderId}_to status:{Status}",
				nameof(UpdateOrderAfterPaid), orderId, orderStatus);

			await using var transaction = await _unitOfWork.BeginTransactionAsync();
			try
			{
				var order = await _orderRepository.GetAll()
					.Include(o => o.Items)
					.FirstOrDefaultAsync(o => o.Id == orderId);

				if (order == null)
				{
					_logger.LogWarning("Can't find order {OrderId}", orderId);
					return Result<bool>.Fail("Can't find order", 404);
				}


                await _unitOfWork.Order.LockOrderForUpdateAsync(
                    order.Id);

                if (!IsValidTransition(order.Status, orderStatus))
				{
					_logger.LogWarning("Invalid status transition from {Old} to {New}", order.Status, orderStatus);
					return Result<bool>.Fail($"Invalid status transition from {order.Status} to {orderStatus}", 400);
				}

				bool shouldReduceStock = false;

				if (orderStatus == OrderStatus.Processing)
				{
				
					if (order.Status != OrderStatus.Processing || order.RestockedAt != null)
					{
						shouldReduceStock = true;
						order.RestockedAt = null;
					}
				}

				order.Status = orderStatus;

				await _unitOfWork.CommitAsync();
				await transaction.CommitAsync();


				if (shouldReduceStock && order.Items.Any())
				{
                    var itemsToReduce = order.Items.ToList();

					_backgroundJobClient.Enqueue(() => ReduceQuantityOfProduct(itemsToReduce));
				}

				_backgroundJobClient.Enqueue(()=> RemoveCacheAndRelated()) ;

				_logger.LogInformation("Updated order {OrderId} to {Status} after payment", orderId, order.Status);
				return Result<bool>.Ok(true, "Order updated after payment", 200);
			}
			catch (DbUpdateConcurrencyException e)
			{
				await transaction.RollbackAsync();
				_logger.LogWarning(e, "Concurrency conflict while updating order {OrderId} after payment", orderId);
				return Result<bool>.Fail("Order was modified by another process.", 409);
			}
			catch (Exception ex)
			{
				await transaction.RollbackAsync();
				_logger.LogError(ex, "Error updating order {OrderId} after payment", orderId);
				_cacheHelper.NotifyAdminError($"Error updating order {orderId} after payment: {ex.Message}", ex.StackTrace);
				return Result<bool>.Fail("An error occurred while updating the order after payment", 500);
			}
		}


		private async Task ReduceQuantityOfProduct(List<OrderItem> orderItems)
        {
            // Use ProductVariantCommandService methods instead of direct manipulation
            foreach (var orderItem in orderItems)
            {
                var removeResult = await _productVariantCommandService.RemoveQuntityAfterOrder(orderItem.ProductVariantId, orderItem.Quantity);
                if (!removeResult.Success)
                {
                    _logger.LogError("Failed to reduce quantity for variant {VariantId}: {Message}", 
                        orderItem.ProductVariantId, removeResult.Message);
                }
            }


		}
        private void RemoveCacheAndRelated()
        {
            _cacheHelper.ClearOrderCache();
			_productVariantCacheHelper.RemoveProductCachesAsync();
            _productCacheManger.ClearProductCache();
            _collectionCacheHelper.ClearCollectionCache();
            _subCategoryCacheHelper.ClearSubCategoryCache();

		}
		private bool IsValidTransition(OrderStatus current, OrderStatus target)
        {
            return current switch
            {
				OrderStatus.PendingPayment => target is OrderStatus.Confirmed or OrderStatus.PaymentExpired or OrderStatus.CancelledByUser or OrderStatus.CancelledByAdmin,
				OrderStatus.Confirmed => target is OrderStatus.Processing or OrderStatus.CancelledByAdmin,
				OrderStatus.Processing => target is OrderStatus.Shipped or OrderStatus.CancelledByAdmin, 
				OrderStatus.Shipped => target is OrderStatus.Delivered,
				OrderStatus.Delivered => target is OrderStatus.Complete or OrderStatus.Returned or OrderStatus.Refunded,
				OrderStatus.PaymentExpired => target is OrderStatus.CancelledByAdmin or OrderStatus.CancelledByUser,
				_ => false

			};
        }

        private async Task<Result<bool>> UpdateStatusAsync(
            int orderId,
            string adminId,
            OrderStatus target,
            string operationTitle,
            string successMessage,
            bool IsSysyem=false,
            bool IsAdmin=false,
            string? notes = null)
        {
            await using var transaction = await _unitOfWork.BeginTransactionAsync();
            try
            {
                var order = await _orderRepository.GetByIdAsync(orderId);
                if (order == null)
                    return Result<bool>.Fail("Order not found", 404);

                await _unitOfWork.Order.LockOrderForUpdateAsync(
                    order.Id);


                if (!IsValidTransition(order.Status, target))
                    return Result<bool>.Fail("Invalid status transition", 400);

                var oldstatus = order.Status;
                order.Status = target;
                if (!IsSysyem)
                {
                    if (!IsAdmin)
                    {
                        if (target == OrderStatus.CancelledByUser)
                        {

                            var log = await _userOpreationServices.AddUserOpreationAsync(
                             $"{operationTitle} order {orderId} from {oldstatus} to {target}",
                             Opreations.UpdateOpreation,
                             adminId,
                             orderId);


                            if (!log.Success)
                            {
                                _logger.LogWarning("User log failed while {Op} order {OrderId}: {Msg}", operationTitle, orderId, log.Message);
                                _cacheHelper.NotifyAdminError($"User log failed while {operationTitle} order {orderId}: {log.Message}");
                            }
                        }
                        else
                        {
                            var log = await _adminOperationServices.AddAdminOpreationAsync(
                            $"{operationTitle} order {orderId}from {oldstatus} to {target}",
                            Opreations.UpdateOpreation,
                            adminId,
                            orderId);

                            if (!log.Success)
                            {
                                _logger.LogWarning("Admin log failed while {Op} order {OrderId}: {Msg}", operationTitle, orderId, log.Message);
                                _cacheHelper.NotifyAdminError($"User log failed while {operationTitle} order {orderId}: {log.Message}");
                            }

                        }
                    }


                }
                else
                {
                    _logger.LogInformation($"Change Order Status from {oldstatus} to {target}");
                }


                if (target == OrderStatus.Shipped)
                    order.ShippedAt = DateTime.UtcNow;

                if (target == OrderStatus.Delivered)
                    order.DeliveredAt = DateTime.UtcNow;

            
                await _unitOfWork.CommitAsync();
                await transaction.CommitAsync();
				RemoveCacheAndRelated();

				return Result<bool>.Ok(true, successMessage, 200);
            }
            catch (DbUpdateConcurrencyException e)
            {
                await transaction.RollbackAsync();
                _logger.LogWarning(e, "Concurrency conflict updating order {OrderId} to {Target}", orderId, target);
                return Result<bool>.Fail("Order was modified by another process.", 409);
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Error updating order {OrderId} to {Target}", orderId, target);
                return Result<bool>.Fail("An error occurred while updating order status", 500);
            }
        }

        public async Task<Result<int>> CountOrdersAsync(
      OrderStatus?status=null,
     bool? isDelete = null,
     bool isAdmin = false)
        {
            _logger.LogInformation(
                "Execute {Method}_Status:{IsActive}_isDelete:{IsDelete}_isAdmin:{IsAdmin}",
                nameof(CountOrdersAsync),
                status,
                isDelete,
                isAdmin
            );

            if (!isAdmin)
            {
                isDelete = false;
            }

            var query = _unitOfWork.Order.GetAll();

            if (isDelete.HasValue)
                query = isDelete.Value
                    ? query.Where(p => p.DeletedAt != null)
                    : query.Where(p => p.DeletedAt == null);

            if(status.HasValue)
            query= query.Where(o => o.Status == status);



            return Result<int>.Ok(await query.CountAsync());
        }

        public Task<Result<bool>> ConfirmOrderAsync(int orderId, string adminId, bool IsSysyem = false, bool IsAdmin = false, string? notes = null)
            => UpdateStatusAsync(orderId, adminId, OrderStatus.Confirmed, "Confirmed", "Order confirmed",IsSysyem,IsAdmin,notes: notes);

        public Task<Result<bool>> ProcessOrderAsync(int orderId, string adminId, string? notes = null)
            => UpdateStatusAsync(orderId, adminId, OrderStatus.Processing, "Processing", "Order set to processing",notes: notes);

        public Task<Result<bool>> RefundOrderAsync(int orderId, string adminId, string? notes = null)
            => UpdateStatusAsync(orderId, adminId, OrderStatus.Refunded, "Refunded", "Order refunded",notes: notes);

        public Task<Result<bool>> ReturnOrderAsync(int orderId, string adminId, string? notes = null)
            => UpdateStatusAsync(orderId, adminId, OrderStatus.Returned, "Returned", "Order marked as returned",notes: notes);

        public Task<Result<bool>> ExpirePaymentAsync(int orderId, string adminId, bool IsSysyem = false, bool IsAdmin = false, string? notes = null)
            => UpdateStatusAsync(orderId, adminId, OrderStatus.PaymentExpired, "Payment expired", "Order payment expired",IsSysyem,IsAdmin, notes);

        public Task<Result<bool>> CompleteOrderAsync(int orderId, string adminId, string? notes = null)
            => UpdateStatusAsync(orderId, adminId, OrderStatus.Complete, "Completed", "Order completed",notes: notes);

        public Task<Result<bool>> ShipOrderAsync(int orderId, string adminId, string? notes = null)
            => UpdateStatusAsync(orderId, adminId, OrderStatus.Shipped, "Shipped", "Order shipped successfully",notes: notes);

        public Task<Result<bool>> DeliverOrderAsync(int orderId, string adminId, string? notes = null)
            => UpdateStatusAsync(orderId, adminId, OrderStatus.Delivered, "Delivered", "Order delivered successfully",notes: notes);

        public Task<Result<bool>> ShipOrderAsync(int orderId, string userId)
            => UpdateStatusAsync(orderId, userId, OrderStatus.Shipped, "Shipped", "Order shipped successfully",notes: null);

        public Task<Result<bool>> DeliverOrderAsync(int orderId, string userId)
            => UpdateStatusAsync(orderId, userId, OrderStatus.Delivered, "Delivered", "Order delivered successfully",notes: null);

        public async Task<Result<bool>> CancelOrderByCustomerAsync(int orderId, string userId)
        {
            await using var transaction = await _unitOfWork.BeginTransactionAsync();
            try
            {
                var order = await _orderRepository.GetByIdAsync(orderId);
                if (order == null || order.CustomerId != userId)
                {
                    await transaction.RollbackAsync();
                    return Result<bool>.Fail("Order not found or access denied", 404);
                }

                await _unitOfWork.Order.LockOrderForUpdateAsync(
                    order.Id);

                if (order.Status == OrderStatus.CancelledByAdmin || order.Status == OrderStatus.CancelledByUser)
                {
                    await transaction.RollbackAsync();
                    return Result<bool>.Fail("Order is already cancelled", 400);
                }

                if (order.Status is not (OrderStatus.PendingPayment or OrderStatus.PaymentExpired))
                {
                    await transaction.RollbackAsync();
                    return Result<bool>.Fail("Order cannot be canceled in its current status", 400);
                }

                order.CancelledAt = DateTime.UtcNow;
                order.Status = OrderStatus.CancelledByUser;

                var operationAdded = await _userOpreationServices.AddUserOpreationAsync(
                    $"Cancelled order {order.Id}",
                    Opreations.UpdateOpreation,
                    userId,
                    orderId
                );

                if (!operationAdded.Success)
                {
                    await transaction.RollbackAsync();
                    return Result<bool>.Fail("Failed to record user operation", 500);
                }

                await _unitOfWork.CommitAsync();
                await transaction.CommitAsync();

                RemoveCacheAndRelated();
                _backgroundJobClient.Enqueue(() => RestockOrderItemsInBackground(orderId));

                return Result<bool>.Ok(true, "Order cancelled successfully", 200);
            }
            catch (DbUpdateConcurrencyException e)
            {
                await transaction.RollbackAsync();
                _logger.LogWarning(e, "Concurrency conflict cancelling order {OrderId} by user {UserId}", orderId, userId);
                return Result<bool>.Fail("Order was modified by another process.", 409);
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Error cancelling order {OrderId} by user {UserId}", orderId, userId);
                _cacheHelper.NotifyAdminError($"Error cancelling order {orderId} by user {userId}: {ex.Message}", ex.StackTrace);
                return Result<bool>.Fail("An error occurred while cancelling the order", 500);
            }
        }

        public async Task<Result<bool>> CancelOrderByAdminAsync(int orderId, string adminId)
        {
            await using var transaction = await _unitOfWork.BeginTransactionAsync();
            try
            {
                var order = await _orderRepository.GetByIdAsync(orderId);
                if (order == null)
                {
                    return Result<bool>.Fail("Order not found", 404);
                }
                await _unitOfWork.Order.LockOrderForUpdateAsync(
                                   order.Id);

                if (order.Status == OrderStatus.Delivered
                    || order.Status == OrderStatus.Refunded
                    || order.Status == OrderStatus.Returned)
                {
                    return Result<bool>.Fail("Can't cancel delivered, refunded, or returned orders", 400);
                }

                if (order.Status == OrderStatus.CancelledByAdmin
                    || order.Status == OrderStatus.CancelledByUser)
                {
                    return Result<bool>.Fail("Order is already cancelled", 400);
                }

                // ✅ Update order
                order.CancelledAt = DateTime.UtcNow;
                order.Status = OrderStatus.CancelledByAdmin;

                await _unitOfWork.CommitAsync();
                await transaction.CommitAsync();

                // ✅ Log admin operation (best-effort)
                var adminOperationResult = await _adminOperationServices.AddAdminOpreationAsync(
                    $"Cancelled order {orderId} by admin {adminId}",
                    Opreations.UpdateOpreation,
                    adminId,
                    orderId);

                if (!adminOperationResult.Success)
                {
                    _logger.LogWarning("Failed to log admin operation for order {OrderId} by admin {AdminId}", orderId, adminId);
                }

                // ✅ Schedule background jobs
                RemoveCacheAndRelated();
                _backgroundJobClient.Enqueue(() => RestockOrderItemsInBackground(orderId));

                return Result<bool>.Ok(true, "Order cancelled by admin", 200);
            }
            catch (DbUpdateConcurrencyException e)
            {
                await transaction.RollbackAsync();
                _logger.LogWarning(e, "Concurrency conflict while cancelling order {OrderId} by admin {AdminId}", orderId, adminId);
                _cacheHelper.NotifyAdminError(e.Message, e.StackTrace);
                return Result<bool>.Fail("Order was modified by another process.", 409);
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Error while cancelling order by admin {AdminId}", adminId);
                _cacheHelper.NotifyAdminError(ex.Message, ex.StackTrace);
                return Result<bool>.Fail("An error occurred while cancelling order", 500);
            }
        }

        public async Task ExpireUnpaidOrderInBackground(int orderId)
        {
            await using var transaction = await _unitOfWork.BeginTransactionAsync();
            try
            {
                var order = await _orderRepository.GetByIdAsync(orderId);
                if (order == null)
                {
                    _logger.LogWarning("Expire skipped: order {OrderId} not found", orderId);
                    return;
                }

                await _unitOfWork.Order.LockOrderForUpdateAsync(
                    order.Id);

                if (order.Status != OrderStatus.PendingPayment)
                {
                    _logger.LogInformation("Expire skipped: order {OrderId} status is {Status}", orderId, order.Status);
                    return;
                }


                if (!IsValidTransition(order.Status, OrderStatus.PaymentExpired))
                {
                    _logger.LogWarning("Cannot change order {OrderId} status to PaymentExpired from {CurrentStatus}", orderId, order.Status);
                    return;
                }

                order.Status = OrderStatus.PaymentExpired;
                await _unitOfWork.CommitAsync();
                await transaction.CommitAsync();
                _backgroundJobClient.Enqueue(() => RestockOrderItemsInBackground(orderId));
                RemoveCacheAndRelated();
                _logger.LogInformation("Order {OrderId} auto-expired and restock scheduled", orderId);
            }
            catch (DbUpdateConcurrencyException e)
            {
                await transaction.RollbackAsync();
                _logger.LogWarning(e, "Concurrency conflict while auto-expiring order {OrderId}", orderId);
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Error while auto-expiring order {OrderId}", orderId);
            }
        }

        public async Task RestockOrderItemsInBackground(int orderId)
        {
            

            try
            {
                var order = await _unitOfWork.Repository<DomainLayer.Models.Order>().GetByIdAsync(orderId);
                if (order == null)
                {
                    _logger.LogWarning("Restock skipped: order {OrderId} not found", orderId);
                    return;
                }

                await _unitOfWork.Order.LockOrderForUpdateAsync(
                   order.Id);

                if (order.RestockedAt.HasValue)
                {
                    _logger.LogInformation("Restock skipped: order {OrderId} already restocked at {RestockedAt}", orderId, order.RestockedAt);
                    return;
                }

                // Only change status if it's still PendingPayment and has expired
                if (order.Status == OrderStatus.PendingPayment &&
                    order.CreatedAt.HasValue &&
                    order.CreatedAt.Value.AddHours(2) < DateTime.UtcNow)
                {
                    // Validate the transition before changing status
                    if (IsValidTransition(order.Status, OrderStatus.PaymentExpired))
                    {
                        order.Status = OrderStatus.PaymentExpired;
                    }
                    else
                    {
                        _logger.LogWarning("Cannot change order {OrderId} status to PaymentExpired from {CurrentStatus}", orderId, order.Status);
                        return;
                    }
                }

                // Allow restock only if status is cancelled/expired
                if (order.Status != OrderStatus.CancelledByAdmin &&
                    order.Status != OrderStatus.CancelledByUser &&
                    order.Status != OrderStatus.PaymentExpired)
                {
                    _logger.LogInformation("Restock skipped: order {OrderId} status is {Status}", orderId, order.Status);
                    return;
                }

                var orderItems = await _unitOfWork.Repository<OrderItem>()
                    .GetAll()
                    .Where(i => i.OrderId == orderId)
                    .Select(i => new { i.ProductVariantId, i.Quantity })
                    .ToListAsync();

                if (!orderItems.Any())
                {
                    _logger.LogInformation("Restock skipped: order {OrderId} has no items", orderId);
                    return;
                }

                // Use ProductVariantCommandService methods instead of direct manipulation
                foreach (var item in orderItems)
                {
                    var addResult = await _productVariantCommandService.AddQuntityAfterRestoreOrder(item.ProductVariantId, item.Quantity);
                    if (!addResult.Success)
                    {
                        _logger.LogError("Failed to restock variant {VariantId}: {Message}", 
                            item.ProductVariantId, addResult.Message);
                    }
                }

                // Mark restocked
                order.RestockedAt = DateTime.UtcNow;

                await _unitOfWork.CommitAsync();
                RemoveCacheAndRelated();

                _logger.LogInformation("Restocked inventory for order {OrderId}", orderId);
            }
            catch (DbUpdateConcurrencyException e)
            {
                _logger.LogWarning(e, "Concurrency conflict while restocking order {OrderId}", orderId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while restocking inventory for order {OrderId}", orderId);

                _backgroundJobClient.Enqueue(() =>
                    _errorNotificationService.SendErrorNotificationAsync(ex.Message, null));
            }
        }
    }
}


