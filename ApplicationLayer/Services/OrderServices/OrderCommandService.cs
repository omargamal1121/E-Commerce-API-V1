using Domain.Enums;
using Application.DtoModels.OrderDtos;
using Application.Interfaces;
using Domain.Models;
using Application.Services.AdminOperationServices;
using Application.Services.CacheServices;
using Application.Services.CollectionServices;
using Application.Services.EmailServices;
using Application.Services.ProductServices;
using Application.Services.ProductVariantServices;
using Application.Services.SubCategoryServices;
using Application.Services.UserOperationServices;
using Hangfire;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using Infrastructure.Interfaces;
using Application.Services.CartServices;

namespace Application.Services.OrderServices
{
    public class OrderCommandService : IOrderCommandService, IDisposable
    {
        private readonly ILogger<OrderCommandService> _logger;
        private readonly IUnitOfWork _unitOfWork;
        private readonly IProductVariantCacheHelper _productVariantCacheHelper;
        private readonly IProductCacheManger _productCacheManger;
        private readonly ICollectionCacheHelper _collectionCacheHelper;
        private readonly ISubCategoryCacheHelper _subCategoryCacheHelper;
        private readonly ICartRepository _cartRepository;
		private readonly IUserOperationServices _UserOperationServices;
        private readonly IOrderRepository _orderRepository;
        private readonly ICartServices _cartServices;
        private readonly IBackgroundJobClient _backgroundJobClient;
        private readonly IErrorNotificationService _errorNotificationService;
        private readonly IAdminOpreationServices _adminOperationServices;
        private readonly IProductCatalogService _productCatalogService;
        private readonly UserManager<Customer> _userManager;
        private readonly IOrderCacheHelper _cacheHelper;
        private readonly IProductVariantCommandService _productVariantCommandService;
        private readonly ICartMapper _cartmapper;
        

        private static readonly ConcurrentDictionary<int, SemaphoreSlim> _variantLocks = new();

        public OrderCommandService(
            ICartRepository cartRepository,
            ISubCategoryCacheHelper subCategoryCacheHelper,
            IProductVariantCacheHelper productVariantCacheHelper,
            IProductCacheManger productCacheManger,
            ICollectionCacheHelper collectionCacheHelper,
            ICartMapper cartMapper ,
			IProductCatalogService productCatalogService,
            IBackgroundJobClient backgroundJobClient,
            IErrorNotificationService errorNotificationService,
            UserManager<Customer> userManager,
            IUserOperationServices UserOperationServices,
            ILogger<OrderCommandService> logger,
            IUnitOfWork unitOfWork,
            IOrderRepository orderRepository,
            ICartServices cartServices,
            IAdminOpreationServices adminOperationServices,
            IOrderCacheHelper cacheHelper,
            IProductVariantCommandService productVariantCommandService)
        {
            _cartmapper = cartMapper;
            _cartRepository = cartRepository;
            _subCategoryCacheHelper = subCategoryCacheHelper;
            _productVariantCacheHelper = productVariantCacheHelper;
            _collectionCacheHelper = collectionCacheHelper;
            _productCacheManger = productCacheManger;
			_productCatalogService = productCatalogService;
            _backgroundJobClient = backgroundJobClient;
            _errorNotificationService = errorNotificationService;
            _userManager = userManager;
            _UserOperationServices = UserOperationServices;
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

            #region Validate User and Cart (Read-only, pre-transaction)
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
                return Result<OrderAfterCreatedto>.Fail("UnAuthorized", 401);

            if (!await _unitOfWork.CustomerAddress.IsExsistByIdAndUserIdAsync(orderDto.AddressId, userId))
                return Result<OrderAfterCreatedto>.Fail("Address doesn't exist", 400);

            var cart = await _cartRepository.GetCartByUserIdAsync(userId);
            if (cart is null)
                return Result<OrderAfterCreatedto>.Fail("Cart is empty", 400);
            #endregion

            await using var transaction = await _unitOfWork.BeginTransactionAsync();

            try
            {
                #region Lock Cart and Fetch Cart Projection
                // Concurrency is handled by EF Core optimistic concurrency tokens (RowVersion) and unique constraints.

                var cartdto = await _cartRepository
					.GetCartForCheckoutNoTrackingQuery(userId)
					.Select(_cartmapper.CartDtoSelector)
					.FirstOrDefaultAsync();

				if (cartdto is null || !cartdto.Items.Any())
				{
					await transaction.RollbackAsync();
					return Result<OrderAfterCreatedto>.Fail("Cart is empty", 400);
				}

				//if (cartdto.CheckoutDate == null || cartdto.CheckoutDate.Value.AddDays(7) < DateTime.UtcNow)
				//{
				//	await transaction.RollbackAsync();
				//	return Result<OrderAfterCreatedto>.Fail("Please checkout before creating order", 400);
				//}
				#endregion

				#region Filter Active Items and Pre-Check Stock Availability
				var activeItems = cartdto.Items
					.Where(i => i.Product.IsActive && i.DeletedAt == null)
					.ToList();

				if (!activeItems.Any())
				{
					await transaction.RollbackAsync();
					return Result<OrderAfterCreatedto>.Fail("No active items found in the cart for checkout.", 400);
				}

				var variantIds = activeItems
					.Select(i => i.Product.productVariantForCartDto.Id)
					.ToList();

				// Fetch current DB quantities with no tracking — safe read inside the locked transaction
				var variants = await _unitOfWork.Repository<ProductVariant>()
					.GetAll()
					.AsNoTracking()
					.Where(v => variantIds.Contains(v.Id))
					.Select(v => new  { v.Id, v.Quantity, v.DeletedAt ,v.Product,v.Product.Discount })
					.ToDictionaryAsync(v=>v.Id);

				// Validate every active cart item against current DB stock
				foreach (var item in activeItems)
				{
					var variantId = item.Product.productVariantForCartDto.Id;
					var variant = variants.GetValueOrDefault(variantId);

					if (variant == null || variant.DeletedAt != null)
					{
						await transaction.RollbackAsync();
						return Result<OrderAfterCreatedto>.Fail(
							$"Product '{item.Product.Name}' variant is no longer available.",
							409
						);
					}

					if (variant.Quantity < item.Quantity)
					{
						await transaction.RollbackAsync();
						return Result<OrderAfterCreatedto>.Fail(
							$"Product '{item.Product.Name}' is not available in required quantity. " +
							$"Requested: {item.Quantity}, Available: {variant.Quantity}",
							409
						);
					}
                    var discountPercent = variant.Product.Discount?.DiscountPercent ?? 0m;
                    var discountActive = variant.Product.Discount != null && 
                                         variant.Product.Discount.IsActive && 
                                         variant.Product.Discount.DeletedAt == null && 
                                         variant.Product.Discount.StartDate <= DateTime.UtcNow && 
                                         variant.Product.Discount.EndDate > DateTime.UtcNow;
                    var calculatedPrice = Math.Round(discountActive 
                        ? variant.Product.Price - (discountPercent / 100m * variant.Product.Price) 
                        : variant.Product.Price, 2);

                    if (item.CurrentPrice != calculatedPrice)
                    {
                        await transaction.RollbackAsync();
                        return Result<OrderAfterCreatedto>.Fail(
                            $"Product '{item.Product.Name}' price has changed. Please review your cart.",
                            409
                        );
                    }
				}
				#endregion

				#region Create Order + Items (Using strictly active items)
				var subtotal = activeItems.Sum(i => i.Quantity * i.CurrentPrice);
                var total = subtotal;

                var orderNumber = await _orderRepository.GenerateOrderNumberAsync();

                var order = new Domain.Models.Order
                {
                    CustomerId = userId,
                    OrderNumber = orderNumber,
                    CustomerAddressId = orderDto.AddressId,
                    Status = OrderStatus.PendingPayment,
                    Subtotal = subtotal,
                    Total = total,
                    Notes = orderDto.Notes,
                    CreatedAt = DateTime.UtcNow,
                    Items = activeItems.Select(item => new OrderItem
					{
						ProductId = item.ProductId,
						ProductVariantId = item.Product.productVariantForCartDto.Id,
						Quantity = item.Quantity,
						UnitPrice = item.CurrentPrice,
						TotalPrice = item.Quantity * item.CurrentPrice,
						OrderedAt = DateTime.UtcNow,
						CreatedAt = DateTime.UtcNow
					}).ToList()
				};

                var createdOrder = await _orderRepository.CreateAsync(order);
                if (createdOrder == null)
                {
                    await transaction.RollbackAsync();
                    return Result<OrderAfterCreatedto>.Fail("Failed to create order", 500);
                }
				#endregion

				#region Reduce Stock After Order Creation
				foreach (var item in activeItems)
				{
					var removeResult = await _productVariantCommandService.RemoveQuntityAfterOrder(
						item.Product.productVariantForCartDto.Id,
						item.Quantity,
						item.ProductId);

					if (!removeResult.Success)
					{
						await transaction.RollbackAsync();
						return Result<OrderAfterCreatedto>.Fail(
							$"Failed to reduce stock for product '{item.Product.Name}': {removeResult.Message}",
							409
						);
					}
				}
				#endregion

				#region Clear Cart + Commit
				await _unitOfWork.Cart.ClearCartAsync(cart.Id);

                await _unitOfWork.CommitAsync();
                await transaction.CommitAsync();
                #endregion

                #region Safe Background Job Scheduling (Post-Commit)
                _backgroundJobClient.Enqueue(() => DeactivateZeroQuantityVariantsAsync(variantIds));

                _backgroundJobClient.Schedule(
                    () => ExpireUnpaidOrderInBackground(createdOrder.Id),
                    TimeSpan.FromMinutes(5)
                );
                #endregion

                #region Log User Operation (best-effort, Post-Commit)
                var logResult = await _UserOperationServices.AddUserOpreationAsync(
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

                var response = new OrderAfterCreatedto 
                { 
                    OrderId = createdOrder.Id,
                    OrderNumber = order.OrderNumber
                };

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

			try
			{
				await _unitOfWork.Order.LockOrderForUpdateAsync(orderId);

				var order = await _orderRepository.GetAll()
					.Include(o => o.Items)
					.FirstOrDefaultAsync(o => o.Id == orderId);

				if (order == null)
				{
					_logger.LogWarning("Can't find order {OrderId}", orderId);
					return Result<bool>.Fail("Can't find order", 404);
				}

                if (!IsValidTransition(order.Status, orderStatus))
				{
					_logger.LogWarning("Invalid status transition from {Old} to {New}", order.Status, orderStatus);
					return Result<bool>.Fail($"Invalid status transition from {order.Status} to {orderStatus}", 400);
				}

				order.Status = orderStatus;

				_logger.LogInformation("Updated order {OrderId} to {Status} after payment", orderId, order.Status);
				return Result<bool>.Ok(true, "Order updated after payment", 200);
			}
			catch (DbUpdateConcurrencyException e)
			{
				_logger.LogWarning(e, "Concurrency conflict while updating order {OrderId} after payment", orderId);
				return Result<bool>.Fail("Order was modified by another process.", 409);
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error updating order {OrderId} after payment", orderId);
				_cacheHelper.NotifyAdminError($"Error updating order {orderId} after payment: {ex.Message}", ex.StackTrace);
				return Result<bool>.Fail("An error occurred while updating the order after payment", 500);
			}
		}

		private async Task ReduceQuantityOfProduct(List<OrderItem> orderItems)
        {
            foreach (var orderItem in orderItems)
            {
                var removeResult = await _productVariantCommandService.RemoveQuntityAfterOrder(orderItem.ProductVariantId, orderItem.Quantity, orderItem.ProductId);
                if (!removeResult.Success)
                {
                    _logger.LogError("Failed to reduce quantity for variant {VariantId}: {Message}", 
                        orderItem.ProductVariantId, removeResult.Message);
                }
            }
        }

		/// <summary>
		/// Deactivates all variants from the given IDs whose quantity has reached zero.
		/// Uses a single bulk ExecuteUpdateAsync — one UPDATE WHERE statement, no entity loading.
		/// Runs as a background job so the order response is not delayed.
		/// </summary>
		public async Task DeactivateZeroQuantityVariantsAsync(List<int> variantIds)
		{
			try
			{
				var affectedRows = await _unitOfWork.Repository<ProductVariant>()
					.GetAll()
					.Where(v => variantIds.Contains(v.Id)
							 && v.DeletedAt == null
							 && v.Quantity == 0
							 && v.IsActive)
					.ExecuteUpdateAsync(setters =>
						setters.SetProperty(v => v.IsActive, false));

				if (affectedRows == 0)
					return;

				_logger.LogInformation(
					"{Count} variant(s) deactivated (zero stock) for variantIds: {Ids}",
					affectedRows,
					string.Join(", ", variantIds));

				_backgroundJobClient.Enqueue(() => _productVariantCacheHelper.RemoveProductCachesAsync());
				_backgroundJobClient.Enqueue(() => _productCacheManger.ClearProductCache());
			}
			catch (Exception ex)
			{
				_logger.LogError(ex,
					"Error in DeactivateZeroQuantityVariantsAsync for variantIds: {Ids}",
					string.Join(", ", variantIds));
			}
		}

        public void RemoveCacheAndRelated()
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
				OrderStatus.PaymentExpired => target is  OrderStatus.CancelledByAdmin or OrderStatus.CancelledByUser,
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

                            var log = await _UserOperationServices.AddUserOpreationAsync(
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
            => UpdateStatusAsync(orderId, userId, OrderStatus.Delivered, "Delivered", "Order delivered successfully", notes: null);

        public async Task<Result<bool>> CancelOrderByCustomerAsync(int orderId, string userId)
        {
            _logger.LogInformation("Customer {UserId} attempting to cancel order {OrderId}", userId, orderId);

            #region Validate Order (Read-only, pre-transaction)
            var order = await _orderRepository.GetByIdAsync(orderId);
            if (order == null || order.CustomerId != userId)
            {
                return Result<bool>.Fail("Order not found or access denied", 404);
            }

            if (order.Status == OrderStatus.CancelledByAdmin || order.Status == OrderStatus.CancelledByUser)
            {
                return Result<bool>.Fail("Order is already cancelled", 400);
            }

            if (order.Status is not (OrderStatus.PendingPayment or OrderStatus.PaymentExpired))
            {
                return Result<bool>.Fail("Order cannot be canceled in its current status", 400);
            }
            #endregion

            await using var transaction = await _unitOfWork.BeginTransactionAsync();
            try
            {
                // Lock row inside transaction scope
                await _unitOfWork.Order.LockOrderForUpdateAsync(order.Id);

                order.CancelledAt = DateTime.UtcNow;
                order.Status = OrderStatus.CancelledByUser;

                await _unitOfWork.CommitAsync();
                await transaction.CommitAsync();

                #region Safe Operations Post-Commit (Best effort, non-blocking)
                _backgroundJobClient.Enqueue(() => RestockOrderItemsInBackground(orderId));

                var operationAdded = await _UserOperationServices.AddUserOpreationAsync(
                    $"Cancelled order {order.Id}",
                    Opreations.UpdateOpreation,
                    userId,
                    orderId
                );

                if (!operationAdded.Success)
                {
                    _logger.LogWarning("Failed to record customer cancellation log for order {OrderId}", orderId);
                }
                #endregion

                RemoveCacheAndRelated();
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
            _logger.LogInformation("Admin {AdminId} attempting to cancel order {OrderId}", adminId, orderId);

            #region Validate Order (Read-only, pre-transaction)
            var order = await _orderRepository.GetByIdAsync(orderId);
            if (order == null)
            {
                return Result<bool>.Fail("Order not found", 404);
            }

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
            #endregion

            await using var transaction = await _unitOfWork.BeginTransactionAsync();
            try
            {
                // Lock row inside transaction scope
                await _unitOfWork.Order.LockOrderForUpdateAsync(order.Id);

                order.CancelledAt = DateTime.UtcNow;
                order.Status = OrderStatus.CancelledByAdmin;

                await _unitOfWork.CommitAsync();
                await transaction.CommitAsync();

                #region Safe Operations Post-Commit (Best effort, non-blocking)
                _backgroundJobClient.Enqueue(() => RestockOrderItemsInBackground(orderId));

                var adminOperationResult = await _adminOperationServices.AddAdminOpreationAsync(
                    $"Cancelled order {orderId} by admin {adminId}",
                    Opreations.UpdateOpreation,
                    adminId,
                    orderId);

                if (!adminOperationResult.Success)
                {
                    _logger.LogWarning("Failed to log admin operation for order {OrderId} by admin {AdminId}", orderId, adminId);
                }
                #endregion

                RemoveCacheAndRelated();
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
            _logger.LogInformation("Evaluating unpaid order {OrderId} for expiration in background", orderId);

            #region Validate Order (Read-only, pre-transaction)
            var order = await _orderRepository.GetByIdAsync(orderId);
            if (order == null)
            {
                _logger.LogWarning("Expire skipped: order {OrderId} not found", orderId);
                return;
            }

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
            #endregion

            await using var transaction = await _unitOfWork.BeginTransactionAsync();
            try
            {
                await _unitOfWork.Order.LockOrderForUpdateAsync(order.Id);

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
            _logger.LogInformation("Running background restock evaluation for order {OrderId}", orderId);

            await using var transaction = await _unitOfWork.BeginTransactionAsync();
            try
            {
                
                await _unitOfWork.Order.LockOrderForUpdateAsync(orderId);

                var order = await _unitOfWork.Repository<Domain.Models.Order>().GetByIdAsync(orderId);
                if (order == null)
                {
                    _logger.LogWarning("Restock skipped: order {OrderId} not found inside transaction scope", orderId);
                    await transaction.RollbackAsync();
                    return;
                }

                if (order.RestockedAt.HasValue)
                {
                    _logger.LogInformation("Restock skipped: order {OrderId} already restocked at {RestockedAt}", orderId, order.RestockedAt);
                    await transaction.RollbackAsync();
                    return;
                }

                // Check for payment expiration transition if necessary
                if (order.Status == OrderStatus.PendingPayment &&
                    order.CreatedAt.HasValue &&
                    order.CreatedAt.Value.AddHours(2) < DateTime.UtcNow)
                {
                    if (IsValidTransition(order.Status, OrderStatus.PaymentExpired))
                    {
                        order.Status = OrderStatus.PaymentExpired;
                    }
                    else
                    {
                        _logger.LogWarning("Cannot change order {OrderId} status to PaymentExpired from {CurrentStatus}", orderId, order.Status);
                        await transaction.RollbackAsync();
                        return;
                    }
                }

                // Allow restock only if status is cancelled or expired
                if (order.Status != OrderStatus.CancelledByAdmin &&
                    order.Status != OrderStatus.CancelledByUser &&
                    order.Status != OrderStatus.PaymentExpired)
                {
                    _logger.LogInformation("Restock skipped: order {OrderId} status is {Status}", orderId, order.Status);
                    await transaction.RollbackAsync();
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
                    await transaction.RollbackAsync();
                    return;
                }

                foreach (var item in orderItems)
                {
                    var addResult = await _productVariantCommandService.AddQuntityAfterRestoreOrder(item.ProductVariantId, item.Quantity);
                    if (!addResult.Success)
                    {
                        _logger.LogError("Failed to restock variant {VariantId}: {Message}", item.ProductVariantId, addResult.Message);
                    }
                }

                order.RestockedAt = DateTime.UtcNow;

                await _unitOfWork.CommitAsync();
                await transaction.CommitAsync();

                RemoveCacheAndRelated();
                _logger.LogInformation("Restocked inventory successfully for order {OrderId}", orderId);
            }
            catch (DbUpdateConcurrencyException e)
            {
                await transaction.RollbackAsync();
                _logger.LogWarning(e, "Concurrency conflict while restocking order {OrderId}", orderId);
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Error while restocking inventory for order {OrderId}", orderId);

                _backgroundJobClient.Enqueue(() =>
                    _errorNotificationService.SendErrorNotificationAsync(ex.Message, null));
            }
        }

        public async Task<Result<OrderAfterCreatedto>> CreateGuestOrderAsync(CreateGuestOrderDto orderDto)
        {
            _logger.LogInformation("Creating guest order");

            var variantIds = orderDto.Items.Select(i => i.ProductVariantId).ToList();

            await using var transaction = await _unitOfWork.BeginTransactionAsync();

            try
            {
                // Fetch product & variant data from DB
                var variants = await _unitOfWork.Repository<ProductVariant>()
                    .GetAll()
                    .AsNoTracking()
                    .Where(v => variantIds.Contains(v.Id) && v.DeletedAt == null && v.IsActive)
                    .Select(v => new { 
                        v.Id, 
                        v.Quantity, 
                        v.ProductId,
                        v.Product.Price, 
                        v.Product.IsActive, 
                        v.Product.Name,
                        Discount = v.Product.Discount != null ? new
                        {
                            v.Product.Discount.EndDate,
                            v.Product.Discount.StartDate,
                            v.Product.Discount.DiscountPercent,
                            v.Product.Discount.DeletedAt,
                            v.Product.Discount.IsActive
                        } : null
                    })
                    .ToDictionaryAsync(v => v.Id);

                decimal subtotal = 0m;
                var orderItems = new List<OrderItem>();

                foreach (var item in orderDto.Items)
                {
                    var variant = variants.GetValueOrDefault(item.ProductVariantId);

                    if (variant == null || !variant.IsActive)
                    {
                        await transaction.RollbackAsync();
                        return Result<OrderAfterCreatedto>.Fail($"Product variant ID {item.ProductVariantId} is not available.", 409);
                    }

                    if (variant.Quantity < item.Quantity)
                    {
                        await transaction.RollbackAsync();
                        return Result<OrderAfterCreatedto>.Fail(
                            $"Product '{variant.Name}' is not available in required quantity. " +
                            $"Requested: {item.Quantity}, Available: {variant.Quantity}",
                            409
                        );
                    }

                    // Calculate price
                    var discountPercent = variant.Discount?.DiscountPercent ?? 0m;
                    var discountActive = variant.Discount != null && 
                                         variant.Discount.IsActive && 
                                         variant.Discount.DeletedAt == null && 
                                         variant.Discount.StartDate <= DateTime.UtcNow && 
                                         variant.Discount.EndDate > DateTime.UtcNow;

                    var calculatedPrice = Math.Round(discountActive 
                        ? variant.Price - (discountPercent / 100m * variant.Price) 
                        : variant.Price, 2);

                    var itemTotal = item.Quantity * calculatedPrice;
                    subtotal += itemTotal;

                    orderItems.Add(new OrderItem
                    {
                        ProductId = variant.ProductId,
                        ProductVariantId = variant.Id,
                        Quantity = item.Quantity,
                        UnitPrice = calculatedPrice,
                        TotalPrice = itemTotal,
                        OrderedAt = DateTime.UtcNow,
                        CreatedAt = DateTime.UtcNow
                    });
                }

                var orderNumber = await _orderRepository.GenerateOrderNumberAsync();

                var order = new Domain.Models.Order
                {
                    CustomerId = null,
                    CustomerAddressId = null,
                    OrderNumber = orderNumber,
                    Status = OrderStatus.PendingPayment,
                    Subtotal = subtotal,
                    Total = subtotal,
                    Notes = orderDto.Notes,
                    CreatedAt = DateTime.UtcNow,
                    Items = orderItems,
                    
                    // Guest Info
                    CustomerName = orderDto.CustomerName,
                    PhoneNumber = orderDto.PhoneNumber,
                    Email = orderDto.Email,
                    Governorate = orderDto.Governorate,
                    City = orderDto.City,
                    Street = orderDto.Street,
                    Building = orderDto.Building,
                 
                };

                var createdOrder = await _orderRepository.CreateAsync(order);
                if (createdOrder == null)
                {
                    await transaction.RollbackAsync();
                    return Result<OrderAfterCreatedto>.Fail("Failed to create order", 500);
                }

                // Reduce stock
                foreach (var item in orderItems)
                {
                    var removeResult = await _productVariantCommandService.RemoveQuntityAfterOrder(
                        item.ProductVariantId,
                        item.Quantity,
                        item.ProductId);

                    if (!removeResult.Success)
                    {
                        await transaction.RollbackAsync();
                        return Result<OrderAfterCreatedto>.Fail($"Failed to reduce stock: {removeResult.Message}", 409);
                    }
                }

                await _unitOfWork.CommitAsync();
                await transaction.CommitAsync();

                // Safe Background Job Scheduling
                _backgroundJobClient.Enqueue(() => DeactivateZeroQuantityVariantsAsync(variantIds));
                _backgroundJobClient.Schedule(
                    () => ExpireUnpaidOrderInBackground(createdOrder.Id),
                    TimeSpan.FromMinutes(5)
                );

                RemoveCacheAndRelated();

                var response = new OrderAfterCreatedto 
                { 
                    OrderId = createdOrder.Id,
                    OrderNumber = order.OrderNumber
                };

                return Result<OrderAfterCreatedto>.Ok(response, "Guest order created successfully", 201);
            }
            catch (DbUpdateConcurrencyException e)
            {
                await transaction.RollbackAsync();
                _logger.LogWarning(e, "Concurrency conflict while creating guest order");
                return Result<OrderAfterCreatedto>.Fail("Order was modified by another process.", 409);
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Exception while creating guest order");
                _cacheHelper.NotifyAdminError($"Exception creating guest order: {ex.Message}", ex.StackTrace);
                return Result<OrderAfterCreatedto>.Fail("An error occurred while creating the order", 500);
            }
        }
    }
}
