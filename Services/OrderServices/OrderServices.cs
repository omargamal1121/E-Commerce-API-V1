using AutoMapper;
using E_Commerce.DtoModels.OrderDtos;
using E_Commerce.DtoModels.ProductDtos;
using E_Commerce.DtoModels.Responses;
using E_Commerce.Enums;
using E_Commerce.ErrorHnadling;
using E_Commerce.Interfaces;
using E_Commerce.Models;
using E_Commerce.Services.AdminOpreationServices;
using E_Commerce.Services.Cache;
using E_Commerce.Services.EmailServices;
using E_Commerce.Services.PayMobServices;
using E_Commerce.Services.UserOpreationServices;
using E_Commerce.UOW;
using Hangfire;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.JsonPatch.Operations;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using StackExchange.Redis;
using System.Linq.Expressions;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using static E_Commerce.Services.PayMobServices.PayMobServices;
using E_Commerce.DtoModels.PaymentDtos;
using E_Commerce.Services.PaymentMethodsServices;
using E_Commerce.Services.PaymentServices;

namespace E_Commerce.Services.Order
{
    public class OrderServices : IOrderServices
    {
        private readonly ILogger<OrderServices> _logger;
        private readonly IMapper _mapper;
        private readonly IUnitOfWork _unitOfWork;
        private readonly IUserOpreationServices _userOpreationServices;
        private readonly IOrderRepository _orderRepository;
        private readonly ICartServices _cartServices;
        private readonly IPaymentMethodsServices _paymentMethodsServices;
        private readonly IPaymentServices _paymentServices;
		private readonly IBackgroundJobClient _backgroundJobClient;
		private readonly IErrorNotificationService _errorNotificationService;
		private readonly IAdminOpreationServices _adminOperationServices;
        private readonly ICacheManager _cacheManager;
        private readonly UserManager<Customer> _userManager;
		private const string CACHE_TAG_ORDER = "order";
		private const string CACHE_TAG_CART = "cart";

		public OrderServices(
            IPaymentMethodsServices paymentMethodsServices,
            IPaymentServices paymentServices,
            IBackgroundJobClient backgroundJobClient,
            IErrorNotificationService errorNotificationService,
            
            UserManager<Customer> userManager,
			IUserOpreationServices userOpreationServices,
			ILogger<OrderServices> logger,
            IMapper mapper,
            IUnitOfWork unitOfWork,
            IOrderRepository orderRepository,
            ICartServices cartServices,
            IAdminOpreationServices adminOperationServices,
            ICacheManager cacheManager)
        {
            _paymentMethodsServices= paymentMethodsServices;
        
            _paymentServices = paymentServices;
            _backgroundJobClient = backgroundJobClient;
            _errorNotificationService = errorNotificationService;
            _userManager = userManager;
			_userOpreationServices = userOpreationServices;
			_logger = logger;
            _mapper = mapper;
            _unitOfWork = unitOfWork;
            _orderRepository = orderRepository;
            _cartServices = cartServices;
            _adminOperationServices = adminOperationServices;
            _cacheManager = cacheManager;
        }
		public static readonly Expression<Func<Models.Order, OrderListDto>> OrderListSelector = o => new OrderListDto
		{
			Id = o.Id,
			OrderNumber = o.OrderNumber,
			CustomerName = o.Customer.Name,
			Status = o.Status.ToString(),
			Total = o.Total,
			CreatedAt = o.CreatedAt.Value,

		};


		private void NotifyAdminOfError(string message, string? stackTrace = null)
        {
            BackgroundJob.Enqueue<IErrorNotificationService>(_ => _.SendErrorNotificationAsync(message, stackTrace));
        }
		public async Task<Result<OrderDto>> GetOrderByIdAsync(int orderId, string userId, bool isAdmin = false)
		{
			_logger.LogInformation("Getting order by ID: {OrderId} for user: {UserId}, IsAdmin: {IsAdmin}", orderId, userId, isAdmin);

			var cacheKey = $"{CACHE_TAG_ORDER}_id_{orderId}_user_{userId}_admin_{isAdmin}";
			var cached = await _cacheManager.GetAsync<OrderDto>(cacheKey);
			if (cached != null)
			{
				_logger.LogInformation("Cache hit for order {OrderId}", orderId);
				return Result<OrderDto>.Ok(cached, "Order retrieved from cache", 200);
			}

			try
			{
				var exists = isAdmin
					? await _orderRepository.IsExsistAsync(orderId) 
					: await _orderRepository.IsExistByIdAndUserId(orderId, userId);

				if (!exists)
				{
					_logger.LogWarning("Order {OrderId} not found or not authorized for user {UserId}", orderId, userId);
					return Result<OrderDto>.Fail("Order not found or access denied", 404);
				}

				var order = await _unitOfWork.Repository<E_Commerce.Models.Order>()
					.GetAll()
					.Where(o => o.Id == orderId)
					.Select(OrderSelector)
					.FirstOrDefaultAsync();

				if (order == null)
				{
					_logger.LogWarning("Order {OrderId} not found after confirmed existence (unexpected)", orderId);
					return Result<OrderDto>.Fail("Order not found", 404);
				}

				BackgroundJob.Enqueue(() => CacheOrderInBackground(cacheKey, order));

				_logger.LogInformation("Order {OrderId} retrieved successfully for user {UserId}", orderId, userId);
				return Result<OrderDto>.Ok(order, "Order retrieved successfully", 200);
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error retrieving order {OrderId} for user {UserId}", orderId, userId);
				NotifyAdminOfError($"Error getting order {orderId}: {ex.Message}", ex.StackTrace);
				return Result<OrderDto>.Fail("Unexpected error while retrieving order", 500);
			}
		}


		public async Task<Result<OrderDto>> GetOrderByNumberAsync(string orderNumber, string userId, bool isAdmin = false)
		{
			_logger.LogInformation("Getting order by number: {OrderNumber} for user: {UserId}, IsAdmin: {IsAdmin}", orderNumber, userId, isAdmin);

			var cacheKey = $"{CACHE_TAG_ORDER}_orderNumber_{orderNumber}_user_{userId}_admin_{isAdmin}";
			var cached = await _cacheManager.GetAsync<OrderDto>(cacheKey);
			if (cached != null)
			{
				_logger.LogInformation("Cache hit for order number {OrderNumber}", orderNumber);
				return Result<OrderDto>.Ok(cached, "Order retrieved from cache", 200);
			}

			try
			{
				bool exists = isAdmin
					? await _orderRepository.IsExistByOrderNumberAsync(orderNumber)
					: await _orderRepository.IsExistByOrderNumberAndUserIdAsync(orderNumber, userId);

				if (!exists)
				{
					_logger.LogWarning("Order with number {OrderNumber} not found or not authorized for user {UserId}", orderNumber, userId);
					return Result<OrderDto>.Fail("Order not found or access denied", 404);
				}

				var order = await _unitOfWork.Repository<E_Commerce.Models.Order>()
					.GetAll()
					.Where(o => o.OrderNumber == orderNumber)
					.Select(OrderSelector)
					.FirstOrDefaultAsync();

				if (order == null)
				{
					_logger.LogWarning("Order with number {OrderNumber} not found after existence check", orderNumber);
					return Result<OrderDto>.Fail("Order not found", 404);
				}

				// If not admin, double-check ownership (defensive)
				if (!isAdmin && order.Customer.Id != userId)
				{
					_logger.LogWarning("User {UserId} tried to access order {OrderNumber} they don't own", userId, orderNumber);
					return Result<OrderDto>.Fail("Access denied", 403);
				}

				BackgroundJob.Enqueue(() => CacheOrderInBackground(cacheKey, order));

				_logger.LogInformation("Order {OrderNumber} retrieved successfully for user {UserId}", orderNumber, userId);
				return Result<OrderDto>.Ok(order, "Order retrieved successfully", 200);
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error getting order by number {OrderNumber} for user {UserId}", orderNumber, userId);
				NotifyAdminOfError($"Error getting order by number {orderNumber}: {ex.Message}", ex.StackTrace);
				return Result<OrderDto>.Fail("Unexpected error while retrieving order", 500);
			}
		}

		public async Task<Result<List<OrderListDto>>> GetCustomerOrdersAsync(string userId, bool isDeleted, int page = 1, int pageSize = 10)
		{
			_logger.LogInformation("Getting orders for customer: {UserId}, Page: {Page}, PageSize: {PageSize}, IsDeleted: {IsDeleted}",
				userId, page, pageSize, isDeleted);

			var cacheKey = $"{CACHE_TAG_ORDER}_customer_{userId}_deleted_{isDeleted}_page_{page}_size_{pageSize}";
			var cached = await _cacheManager.GetAsync<List<OrderListDto>>(cacheKey);
			if (cached != null)
			{
				_logger.LogInformation("Cache hit for customer {UserId} orders on page {Page}", userId, page);
				return Result<List<OrderListDto>>.Ok(cached, "Customer orders retrieved from cache", 200);
			}

			try
			{
				var query = _unitOfWork.Repository<E_Commerce.Models.Order>()
					.GetAll();

				query = isDeleted
					? query.Where(o => o.CustomerId == userId && o.DeletedAt != null)
					: query.Where(o => o.CustomerId == userId && o.DeletedAt == null);

				var customerOrders = await query
					.OrderByDescending(o => o.CreatedAt)
					.Select(OrderListSelector)
					.Skip((page - 1) * pageSize)
					.Take(pageSize)
					.ToListAsync();

				if (!customerOrders.Any())
				{
					_logger.LogInformation("No orders found for customer {UserId}", userId);
					return Result<List<OrderListDto>>.Fail("No Orders Found", 200);
				}

				BackgroundJob.Enqueue(() => CacheOrderListInBackground(cacheKey, customerOrders));

				_logger.LogInformation("Successfully retrieved {Count} orders for customer {UserId}", customerOrders.Count, userId);
				return Result<List<OrderListDto>>.Ok(customerOrders, "Customer orders retrieved successfully", 200);
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error getting customer orders for {UserId}", userId);
				NotifyAdminOfError($"Error getting customer orders for {userId}: {ex.Message}", ex.StackTrace);
				return Result<List<OrderListDto>>.Fail("An error occurred while retrieving customer orders", 500);
			}
		}

		public async Task<Result<List<OrderListDto>>> GetOrdersByStatusAsync(OrderStatus status, int page = 1, int pageSize = 10)
        {
            _logger.LogInformation($"Getting orders by status: {status} ");

            var cacheKey = $"{CACHE_TAG_ORDER}_status_{status}_page_{page}_size_{pageSize}";
            var cached = await _cacheManager.GetAsync<List<OrderListDto>>(cacheKey);
            if (cached != null)
            {
                _logger.LogInformation($"Cache hit for orders with status {status}, page {page}");
                return Result<List<OrderListDto>>.Ok(cached, $"Orders with status {status} retrieved from cache", 200);
            }

            try
            {
                var orders = await _unitOfWork.Repository<E_Commerce.Models.Order>()
                    .GetAll()
                    .Where(o => o.DeletedAt == null && o.Status == status)
                    .OrderByDescending(o => o.CreatedAt)
                    .Select(OrderListSelector)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToListAsync();

                BackgroundJob.Enqueue(() => CacheOrderListInBackground(cacheKey, orders));

                return Result<List<OrderListDto>>.Ok(orders, $"Orders with status {status} retrieved successfully", 200);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error getting orders by status {status}: {ex.Message}");
                NotifyAdminOfError($"Error getting orders by status {status}: {ex.Message}", ex.StackTrace);
                return Result<List<OrderListDto>>.Fail("An error occurred while retrieving orders", 500);
            }
        }

		public async Task<Result<string>> RegeneratePaymentLink(int orderId, string userId)
		{
			var order = await _unitOfWork.Order
				.GetAll()
				.Where(o => o.Id == orderId &&
							o.DeletedAt == null &&
							(o.Status == OrderStatus.PendingPayment || o.Status == OrderStatus.PaymentExpired))
				.Select(o => new
				{
					o.CustomerId,
					o.CustomerAddressId,
					o.Id,
					o.Subtotal,
					Method = o.Payment.PaymentMethod.Method
				})
				.FirstOrDefaultAsync();

			if (order == null)
				return Result<string>.Fail("Can't find order");


			var orderItems = await _unitOfWork.Repository<OrderItem>()
				.GetAll()
				.Where(i => i.OrderId == orderId)
				.Select(i => new { i.ProductVariantId, i.Quantity })
				.ToListAsync();

			foreach (var item in orderItems)
			{
				var variant = await _unitOfWork.Repository<ProductVariant>().GetByIdAsync(item.ProductVariantId);
				if (variant == null || variant.Quantity < item.Quantity)
					return Result<string>.Fail("Product is no longer available in required quantity");
			}

			string paymentUrl = string.Empty;

			var createPayment = new CreatePayment
			{
				CustomerId = order.CustomerId,
				AddressId = order.CustomerAddressId,
				OrderId = order.Id,
				Amount = order.Subtotal,
				PaymentMethod = order.Method,
				Currency = "EGP",
			};

			var paymentCreateResult = await _paymentServices.CreatePaymentMethod(createPayment, userId);
			if (!paymentCreateResult.Success)
				return Result<string>.Fail("Failed to initiate payment", 500);

			if (paymentCreateResult.Data != null && paymentCreateResult.Data.IsRedirectRequired)
				paymentUrl = paymentCreateResult.Data.RedirectUrl ?? string.Empty;

			var orderEntity = await _unitOfWork.Repository<Models. Order>().GetByIdAsync(orderId);
			if (orderEntity != null)
			{
				orderEntity.Status = OrderStatus.PendingPayment;
				await _unitOfWork.CommitAsync();
			}

			return Result<string>.Ok(paymentUrl);
		}

		public async Task<Result<OrderWithPaymentDto>> CreateOrderFromCartAsync(string userId, CreateOrderDto orderDto)
        {
            _logger.LogInformation("Creating order from cart for user: {UserId}", userId);

            await using var transaction = await _unitOfWork.BeginTransactionAsync();

            try
            {
                #region Validate User and Cart
                var user = await _userManager.FindByIdAsync(userId);
                if (user == null)
                    return Result<OrderWithPaymentDto>.Fail("UnAuthorized", 401);

                if (!await _unitOfWork.Cart.IsExsistByUserId(userId))
                    return Result<OrderWithPaymentDto>.Fail("Cart is empty", 400);

                if (!await _unitOfWork.CustomerAddress.IsExsistByIdAndUserIdAsync(orderDto.AddressId, userId))
                    return Result<OrderWithPaymentDto>.Fail("Address doesn't exsist", 400);

                var cartResult = await _cartServices.GetCartAsync(userId);
                if (!cartResult.Success || cartResult.Data == null)
                    return Result<OrderWithPaymentDto>.Fail("Failed to retrieve cart", 400);
                if (cartResult.Data.IsEmpty)
                    return Result<OrderWithPaymentDto>.Fail("Failed to Empty cart", 400);

                var cart = cartResult.Data;

                if (cart.CheckoutDate == null || cart.CheckoutDate.Value.AddDays(7) < DateTime.UtcNow)
                    return Result<OrderWithPaymentDto>.Fail("Please Make Checkout on Cart", 400);
                #endregion

                #region Calculate Order Totals
                var subtotal = cart.Items.Sum(i => i.Quantity * i.UnitPrice);
                var total = subtotal;
                int amountInCents = (int)(total * 100);
                #endregion

                #region Create Order and Items
                var orderNumber = await _orderRepository.GenerateOrderNumberAsync();

                var order = new E_Commerce.Models.Order
                {
                    CustomerId = userId,
                    OrderNumber = orderNumber,
                    CustomerAddressId = orderDto.AddressId,
                    Status = OrderStatus.PendingPayment,
                    Subtotal = subtotal,
                    Total = total,
                    Notes = orderDto.Notes,
                    CreatedAt = DateTime.UtcNow,

                };

                var createdOrder = await _orderRepository.CreateAsync(order);
                if (createdOrder == null)
                {
                    await transaction.RollbackAsync();
                    return Result<OrderWithPaymentDto>.Fail("Failed to create order", 500);
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
                }).ToArray();

                await _unitOfWork.Repository<OrderItem>().CreateRangeAsync(orderItems);
                await _unitOfWork.Cart.ClearCartAsync(cart.Id);

                // Clear cache
                await _cacheManager.RemoveByTagAsync(CACHE_TAG_CART);
                #endregion

                await _unitOfWork.CommitAsync();
                #region Log User Operation
                var logResult = await _userOpreationServices.AddUserOpreationAsync(
                    $"Created order {orderNumber} from cart",
                    Opreations.AddOpreation,
                    userId,
                    createdOrder.Id
                );

                if (!logResult.Success)
                {
                    await transaction.RollbackAsync();
                    return Result<OrderWithPaymentDto>.Fail("An error occurred while creating the order", 500);
                }
                #endregion
                string paymentUrl = string.Empty;

                var createPayment = new CreatePayment
                {
                    CustomerId = userId,
                    AddressId = orderDto.AddressId,
                    OrderId = createdOrder.Id,
                    Amount = total,
                    PaymentMethod = orderDto.paymentMethod,
                    Currency = "EGP",
                    Notes = orderDto.Notes,
					WalletPhoneNumber=orderDto.WalletPhoneNumber
                };

                var paymentCreateResult = await _paymentServices.CreatePaymentMethod(createPayment, userId);
                if (!paymentCreateResult.Success)
                {
                    await transaction.RollbackAsync();
                    return Result<OrderWithPaymentDto>.Fail("Failed to initiate payment", 500);
                }
                if (paymentCreateResult.Data != null && paymentCreateResult.Data.IsRedirectRequired)
                {
                    paymentUrl = paymentCreateResult.Data.RedirectUrl ?? string.Empty;
                }

                #region Finalize and Return
                await _unitOfWork.CommitAsync();
                await transaction.CommitAsync();

                var mappedOrderDto = await _unitOfWork.Order
                    .GetAll()
                    .Where(o => o.Id == createdOrder.Id)
                    .Select(OrderSelector)
                    .FirstOrDefaultAsync();

                if (mappedOrderDto == null)
                {
                    _logger.LogError("Failed to retrieve created order DTO for order {OrderId}", createdOrder.Id);
                    return Result<OrderWithPaymentDto>.Fail("Failed to retrieve created order", 500);
                }

                await _cacheManager.RemoveByTagAsync(CACHE_TAG_ORDER);
                var response = new OrderWithPaymentDto { Order = mappedOrderDto, PaymentUrl = paymentUrl };

                return Result<OrderWithPaymentDto>.Ok(response, "Order created successfully", 201);
                #endregion
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Exception while creating order for user {UserId}", userId);
                NotifyAdminOfError($"Exception creating order for user {userId}: {ex.Message}", ex.StackTrace);
                return Result<OrderWithPaymentDto>.Fail("An error occurred while creating the order", 500);
            }
        }

		public async Task<Result<int>> UpdateOrderAfterPaid(int orderId, OrderStatus newStatus)
		{
			var order = await _unitOfWork.Repository<Models.Order>().GetByIdAsync(orderId);

			if (order != null)
			{
				
				order.Status = newStatus;
				_logger.LogInformation("Updated order {OrderId} with status {Status}",
					order.Id, newStatus);

				return Result<int>.Ok(orderId);
			}

			return Result<int>.Fail("Can't find order");
		}


		private static readonly Expression<Func<E_Commerce.Models.Order, OrderDto>> OrderSelector = order => new OrderDto
        {
            Id = order.Id,
            CreatedAt = order.CreatedAt,
            ModifiedAt = order.ModifiedAt,
            OrderNumber = order.OrderNumber,
            Status = order.Status.ToString(),
            Subtotal = order.Subtotal,
            TaxAmount = order.TaxAmount,
            ShippingCost = order.ShippingCost,
            DiscountAmount = order.DiscountAmount,
            Total = order.Total,
            Notes = order.Notes,
			DeletedAt=order.DeletedAt,
            ShippedAt = order.ShippedAt,
            DeliveredAt = order.DeliveredAt,
            CancelledAt = order.CancelledAt,

            Customer = order.Customer == null ? null : new CustomerDto
            {
                Id = order.Customer.Id,
                FullName = order.Customer.Name,
                Email = order.Customer.Email,
                PhoneNumber = order.Customer.PhoneNumber
            },

            Items = order.Items.Select(item => new OrderItemDto
            {
                Id = item.Id,
                CreatedAt = item.CreatedAt,
                ModifiedAt = item.ModifiedAt,
                Quantity = item.Quantity,
                UnitPrice = item.UnitPrice,
                TotalPrice = item.TotalPrice,
                OrderedAt = item.OrderedAt,
                Product = new ProductForCartDto
                {
                    Id = item.Product.Id,
                    Name = item.Product.Name,
                    Price = item.Product.Price,
                    IsActive = item.Product.IsActive,
                    FinalPrice = (item.Product.Discount != null && item.Product.Discount.IsActive && (item.Product.Discount.DeletedAt == null) && (item.Product.Discount.EndDate > DateTime.UtcNow)) ? Math.Round(item.Product.Price - (((item.Product.Discount.DiscountPercent) / 100) * item.Product.Price)) : item.Product.Price,
                    DiscountPrecentage = (item.Product.Discount != null && item.Product.Discount.IsActive && item.Product.Discount.EndDate > DateTime.UtcNow) ? item.Product.Discount.DiscountPercent : 0,

                    MainImageUrl = item.Product.Images.FirstOrDefault(img => img.DeletedAt == null).Url ?? string.Empty,
                    productVariantForCartDto = new ProductVariantForCartDto
                    {
                        Id = item.ProductVariantId,
                        Color = item.ProductVariant.Color,
                        CreatedAt = item.ProductVariant.CreatedAt ?? DateTime.MinValue,
                        ModifiedAt = item.ProductVariant.ModifiedAt ?? DateTime.MinValue,
                        Size = item.ProductVariant.Size,
                        DeletedAt = item.ProductVariant.DeletedAt,
                        Length = item.ProductVariant.Length ?? 0,
                        Quantity = item.Quantity,
                        Waist = item.ProductVariant.Waist ?? 0
                    }

                }
            }).ToList(),

            Payment = order.Payment == null ? null : new PaymentDto
            {
                Id = order.Payment.Id,
                CreatedAt = order.Payment.CreatedAt,
                ModifiedAt = order.Payment.ModifiedAt,
                CustomerId = order.Payment.CustomerId,
                PaymentMethodId = order.Payment.PaymentMethodId,
                PaymentMethod = order.Payment.PaymentMethod == null ? null : new PaymentMethodDto
                {
                    Id = order.Payment.PaymentMethod.Id,
                    CreatedAt = order.Payment.PaymentMethod.CreatedAt,
                    ModifiedAt = order.Payment.PaymentMethod.ModifiedAt,
                    Name = order.Payment.PaymentMethod.Name,

                },
                PaymentProviderId = order.Payment.PaymentProviderId,
                PaymentProvider = order.Payment.PaymentProvider == null ? null : new PaymentProvidersServices.PaymentProviderDto
                {
                    Id = order.Payment.PaymentProvider.Id,
                    CreatedAt = order.Payment.PaymentProvider.CreatedAt.Value,
                    UpdatedAt = order.Payment.PaymentProvider.ModifiedAt,
                    Name = order.Payment.PaymentProvider.Name,
                },
                Amount = order.Payment.Amount,
                PaymentDate = order.Payment.PaymentDate,
                OrderId = order.Payment.OrderId,
                DeletedAt = order.Payment.DeletedAt,


            }
        };
        
        public async Task CacheOrderInBackground(string cacheKey, OrderDto order)
        {
            try
            {
                await _cacheManager.SetAsync(cacheKey, order, tags: new[] { CACHE_TAG_ORDER });
                _logger.LogInformation($"Successfully cached order with key: {cacheKey}");
            }
            catch (Exception ex)
            {
                _logger.LogWarning($"Failed to cache order with key {cacheKey}: {ex.Message}");
            }
        }

        public async Task CacheOrderListInBackground(string cacheKey, List<OrderListDto> orders)
        {
            try
            {
                await _cacheManager.SetAsync(cacheKey, orders, tags: new[] { CACHE_TAG_ORDER });
                _logger.LogInformation($"Successfully cached order list with key: {cacheKey}");
            }
            catch (Exception ex)
            {
                _logger.LogWarning($"Failed to cache order list with key {cacheKey}: {ex.Message}");
            }
        }

        public async Task CacheOrderCountInBackground(string cacheKey, int? count, TimeSpan expiration)
        {
            try
            {
                await _cacheManager.SetAsync(cacheKey, count, expiration, tags: new[] { CACHE_TAG_ORDER });
                _logger.LogInformation($"Successfully cached order count with key: {cacheKey}");
            }
            catch (Exception ex)
            {
                _logger.LogWarning($"Failed to cache order count with key {cacheKey}: {ex.Message}");
            }
        }

        public async Task CacheRevenueInBackground(string cacheKey, decimal revenue, TimeSpan expiration)
        {
            try
            {
                await _cacheManager.SetAsync(cacheKey, revenue, expiration, tags: new[] { CACHE_TAG_ORDER });
                _logger.LogInformation($"Successfully cached revenue with key: {cacheKey}");
            }
            catch (Exception ex)
            {
                _logger.LogWarning($"Failed to cache revenue with key {cacheKey}: {ex.Message}");
            }
        }
		public async Task<Result<bool>> UpdateOrderStatusAsync(int orderId, UpdateOrderStatusDto statusDto)
		{
			_logger.LogInformation($"Updating order {orderId} status to {statusDto.Status}");



			using var transaction = await _unitOfWork.BeginTransactionAsync();
			try
			{
				var updateResult = await _orderRepository.UpdateOrderStatusAsync(orderId, statusDto.Status, statusDto.Notes);
				if (!updateResult)
				{
					await transaction.RollbackAsync();
					return Result<bool>.Fail("Failed to update order status", 500);
				}


				var adminLog = await _adminOperationServices.AddAdminOpreationAsync(
					$"Updated order {orderId} status to {statusDto.Status}",
					Opreations.UpdateOpreation,
					"Admin",
					orderId
				);

				if (!adminLog.Success)
				{
					_logger.LogWarning($"Failed to log admin operation: {adminLog.Message}");
				}

				await _unitOfWork.CommitAsync();
				await transaction.CommitAsync();
				await _cacheManager.RemoveByTagAsync(CACHE_TAG_ORDER);

				

				return Result<bool>.Ok(true, "Order status updated successfully", 200);
			}
			catch (Exception ex)
			{
				await transaction.RollbackAsync();
				_logger.LogError($"Error updating order status for order {orderId}: {ex.Message}");
				NotifyAdminOfError($"Error updating order status for order {orderId}: {ex.Message}", ex.StackTrace);
				return Result<bool>.Fail("An error occurred while updating order status", 500);
			}
		}

		public async Task<Result<string>> CancelOrderAsync(int orderId, CancelOrderDto cancelDto, string userId)
		{
			_logger.LogInformation($"Cancelling order {orderId} by user {userId}");

			using var transaction = await _unitOfWork.BeginTransactionAsync();
			try
			{
				var order = await _orderRepository.GetOrderByIdAsync(orderId);
				if (order == null)
				{
					await transaction.RollbackAsync();
					return Result<string>.Fail("Order not found", 404);
				}

				// Check if user has access to this order
				if (order.Customer.Id != userId)
				{
					await transaction.RollbackAsync();
					return Result<string>.Fail("Access denied", 403);
				}

				var cancelResult = await _orderRepository.CancelOrderAsync(orderId, cancelDto.CancellationReason);
				if (!cancelResult)
				{
					await transaction.RollbackAsync();
					return Result<string>.Fail("Failed to cancel order", 500);
				}

				// Log admin operation
				var adminLog = await _adminOperationServices.AddAdminOpreationAsync(
					$"Cancelled order {orderId}",
					Opreations.UpdateOpreation,
					userId,
					orderId
				);

				if (!adminLog.Success)
				{
					_logger.LogWarning($"Failed to log admin operation: {adminLog.Message}");
				}

				await _unitOfWork.CommitAsync();
				await transaction.CommitAsync();

				// Clear cache
				await _cacheManager.RemoveByTagAsync(CACHE_TAG_ORDER);

				return Result<string>.Ok(null, "Order cancelled successfully", 200);
			}
			catch (Exception ex)
			{
				await transaction.RollbackAsync();
				_logger.LogError($"Error cancelling order {orderId}: {ex.Message}");
				NotifyAdminOfError($"Error cancelling order {orderId}: {ex.Message}", ex.StackTrace);
				return Result<string>.Fail("An error occurred while cancelling order", 500);
			}
		}

		public async Task<Result<string>> ShipOrderAsync(int orderId)
		{

			try
			{
				var shipResult = await _orderRepository.ShipOrderAsync(orderId);
				if (!shipResult)
				{
					return Result<string>.Fail("Failed to ship order", 500);
				}

				await _cacheManager.RemoveByTagAsync(CACHE_TAG_ORDER);
				return Result<string>.Ok(null, "Order shipped successfully", 200);
			}
			catch (Exception ex)
			{
				_logger.LogError($"Error shipping order {orderId}: {ex.Message}");
				return Result<string>.Fail("An error occurred while shipping order", 500);
			}
		}

		public async Task<Result<string>> DeliverOrderAsync(int orderId)
		{
			try
			{
				var deliverResult = await _orderRepository.DeliverOrderAsync(orderId);
				if (!deliverResult)
				{
					return Result<string>.Fail("Failed to deliver order", 500);
				}

				await _cacheManager.RemoveByTagAsync(CACHE_TAG_ORDER);
				return Result<string>.Ok(null, "Order delivered successfully", 200);
			}
			catch (Exception ex)
			{
				_logger.LogError($"Error delivering order {orderId}: {ex.Message}");
				return Result<string>.Fail("An error occurred while delivering order", 500);
			}
		}

		public async Task<Result<int?>> GetOrderCountByCustomerAsync(string userId)
		{
			var cacheKey = $"{CACHE_TAG_ORDER}_count_customer_{userId}";
			var cached = await _cacheManager.GetAsync<int?>(cacheKey);
			if (cached != null)
			{
				_logger.LogInformation($"Cache hit for order count for customer {userId}");
				return Result<int?>.Ok(cached, "Order count retrieved from cache", 200);
			}

			try
			{
				var count = await _orderRepository.GetOrderCountByCustomerAsync(userId);

				// Set cache in background using Hangfire
				BackgroundJob.Enqueue(() => CacheOrderCountInBackground(cacheKey, count, TimeSpan.FromMinutes(15)));

				return Result<int?>.Ok(count, "Order count retrieved", 200);
			}
			catch (Exception ex)
			{
				_logger.LogError($"Error getting order count for user {userId}: {ex.Message}");
				return Result<int?>.Fail("An error occurred while getting order count", 500);
			}
		}

		public async Task<Result<decimal>> GetTotalRevenueByCustomerAsync(string userId)
		{
			var cacheKey = $"{CACHE_TAG_ORDER}_revenue_customer_{userId}";
			var cached = await _cacheManager.GetAsync<decimal?>(cacheKey);
			if (cached != null)
			{
				_logger.LogInformation($"Cache hit for total revenue for customer {userId}");
				return Result<decimal>.Ok(cached.Value, "Total revenue retrieved from cache", 200);
			}

			try
			{
				var revenue = await _orderRepository.GetTotalRevenueByCustomerAsync(userId);

				// Set cache in background using Hangfire
				BackgroundJob.Enqueue(() => CacheRevenueInBackground(cacheKey, revenue, TimeSpan.FromMinutes(20)));

				return Result<decimal>.Ok(revenue, "Total revenue retrieved", 200);
			}
			catch (Exception ex)
			{
				_logger.LogError($"Error getting total revenue for user {userId}: {ex.Message}");
				return Result<decimal>.Fail("An error occurred while getting total revenue", 500);
			}
		}
		public async Task<Result<bool>> CancelOrderByCustomerAsync(int orderId, string userId)
		{
			using var transaction = await _unitOfWork.BeginTransactionAsync();
			try
			{
				var order = await _orderRepository.GetByIdAsync(orderId);
				if (order == null || order.CustomerId != userId)
					return Result<bool>.Fail("Order not found or access denied", 404);

				if (order.Status != OrderStatus.PendingPayment)
					return Result<bool>.Fail("You can't cancel this order", 400);

				order.Status = OrderStatus.Cancelled;

				// Add user operation
				var operationAdded = await _userOpreationServices.AddUserOpreationAsync($"Cancelled order {order.Id}", Opreations.UpdateOpreation, userId, orderId);
				if (operationAdded == null)
				{
					await transaction.RollbackAsync();
					return Result<bool>.Fail("Failed to record user operation", 500);
				}

				await _unitOfWork.CommitAsync();
				await transaction.CommitAsync();

				BackgroundJob.Enqueue(() => InvalidateCacheInBackground());

				return Result<bool>.Ok(true, "Order canceled successfully");
			}
			catch (Exception ex)
			{
				await transaction.RollbackAsync();
				_logger.LogError(ex, $"Error cancelling order {orderId} by user {userId}");
				return Result<bool>.Fail("Something went wrong while canceling the order", 500);
			}
		}


		public async Task<Result<bool>> CancelOrderByAdminAsync(int orderId, string adminId)
		{
			var transaction = await _unitOfWork.BeginTransactionAsync();
			try
			{
				// Check if order exists first
				var order = await _orderRepository.GetByIdAsync(orderId);
				if (order == null)
				{
					await transaction.RollbackAsync();
					return Result<bool>.Fail("Order not found", 404);
				}

				if (order.Status == OrderStatus.Delivered || order.Status == OrderStatus.Refunded)
				{
					await transaction.RollbackAsync();
					return Result<bool>.Fail("Can't cancel delivered or refunded orders", 400);
				}

				order.Status = OrderStatus.Cancelled;
				await _unitOfWork.CommitAsync(); // Save order status change first

				// Log admin operation
				var adminOperationResult = await _adminOperationServices.AddAdminOpreationAsync(


					 $"Cancelled order {orderId} by admin {adminId}", Opreations.UpdateOpreation, adminId, orderId);


				if (adminOperationResult == null)
				{
					await transaction.RollbackAsync();
					return Result<bool>.Fail("Failed to log admin operation", 500);
				}

				await _unitOfWork.CommitAsync();
				await transaction.CommitAsync();

				BackgroundJob.Enqueue(() => InvalidateCacheInBackground());

				return Result<bool>.Ok(true, "Order canceled by admin", 200);
			}
			catch (Exception ex)
			{
				await transaction.RollbackAsync();
				_logger.LogError(ex, "Error while cancelling order by admin");
				return Result<bool>.Fail("An error occurred while cancelling order", 500);
			}
		}



		public async Task<Result<decimal>> GetTotalRevenueByDateRangeAsync(DateTime startDate, DateTime endDate, string userRole)
		{
			if (userRole != "Admin")
			{
				return Result<decimal>.Fail("Unauthorized access", 403);
			}

			try
			{
				var revenue = await _orderRepository.GetTotalRevenueByDateRangeAsync(startDate, endDate);
				return Result<decimal>.Ok(revenue, "Total revenue retrieved", 200);
			}
			catch (Exception ex)
			{
				_logger.LogError($"Error getting total revenue for date range: {ex.Message}");
				return Result<decimal>.Fail("An error occurred while getting total revenue", 500);
			}
		}


		public async Task<Result<int?>> GetTotalOrderCountAsync(OrderStatus? status, string userRole)
		{
			if (userRole != "Admin")
			{
				return Result<int?>.Fail("Unauthorized access", 403);
			}

			try
			{
				var count = await _orderRepository.GetTotalOrderCountAsync(status);
				return Result<int?>.Ok(count, "Total order count retrieved", 200);
			}
			catch (Exception ex)
			{
				_logger.LogError($"Error getting total order count: {ex.Message}");
				return Result<int?>.Fail("An error occurred while getting total order count", 500);
			}
		}

		public async Task<Result<List<OrderListDto>>> FilterOrdersAsync(
			string? userId = null,
			bool? deleted = null,
			int page = 1,
			int pageSize = 10,
			OrderStatus? status = null)
		{
			_logger.LogInformation($"Filtering orders - UserId: {userId}, Deleted: {deleted}, Page: {page}, PageSize: {pageSize}, Status: {status}");

			// Create cache key based on all filter parameters
			var cacheKey = $"{CACHE_TAG_ORDER}_filter_user_{userId ?? "all"}_deleted_{deleted?.ToString() ?? "all"}_page_{page}_size_{pageSize}_status_{status?.ToString() ?? "all"}";
			var cached = await _cacheManager.GetAsync<List<OrderListDto>>(cacheKey);
			if (cached != null)
			{
				_logger.LogInformation($"Cache hit for filtered orders with key: {cacheKey}");
				return Result<List<OrderListDto>>.Ok(cached, "Filtered orders retrieved from cache", 200);
			}

			try
			{
				var mappedOrderDto = _unitOfWork.Order
						.GetAll();
				if (userId is not null)
					mappedOrderDto.Where(o => o.CustomerId == userId);

				if(deleted is not null)
				{
					if(deleted.Value)
						mappedOrderDto.Where(o => o.DeletedAt != null);
					else
						mappedOrderDto.Where(o => o.DeletedAt != null);

				}
				if(status is not null)
					mappedOrderDto.Where(o => o.Status == status.Value);
				  

				var orders= await mappedOrderDto.Select(OrderListSelector)
					.Skip((page - 1) * pageSize)
					.Take(pageSize)
					.ToListAsync();






				if (!orders.Any())
				{
					return Result<List<OrderListDto>>.Ok(new List<OrderListDto>(), "No orders found matching the criteria", 200);
				}


				BackgroundJob.Enqueue(() => CacheOrderListInBackground(cacheKey, orders));

				return Result<List<OrderListDto>>.Ok(orders, "Filtered orders retrieved successfully", 200);
			}
			catch (Exception ex)
			{
				_logger.LogError($"Error filtering orders: {ex.Message}");
				NotifyAdminOfError($"Error filtering orders: {ex.Message}", ex.StackTrace);
				return Result<List<OrderListDto>>.Fail("An error occurred while filtering orders", 500);
			}
		}





		
	

	
	
		public async Task InvalidateCacheInBackground()
		{
			try
			{
				await _cacheManager.RemoveByTagAsync(CACHE_TAG_ORDER);
				_logger.LogInformation("Successfully invalidated order cache");
			}
			catch (Exception ex)
			{
				_logger.LogWarning($"Failed to invalidate cache: {ex.Message}");
			}
		}

		public async Task UpdateOrderItemsForProductsAfterAddDiscountAsync(List<int> productIds, decimal discountPercent)
		{
			var orderItems = await _unitOfWork.Repository<OrderItem>()
	.GetAll()
	.Where(ci => productIds.Contains(ci.ProductId))
	.Include(ci => ci.Product) 
	.ToListAsync();

			foreach (var item in orderItems)
			{
				var newPrice = item.Product.Price - item.Product.Price * discountPercent / 100;
				if (item.UnitPrice != newPrice)
				{
					item.UnitPrice = newPrice;
					item.ModifiedAt = DateTime.UtcNow;
				}
			}

			_unitOfWork.Repository<OrderItem>().UpdateList(orderItems);
			await _unitOfWork.CommitAsync();


			_ = _cacheManager.RemoveByTagAsync(CACHE_TAG_ORDER);
		}

		public async Task UpdateOrderItemsForProductsAfterRemoveDiscountAsync(List<int> productIds)
		{
			var orderItems = await _unitOfWork.Repository<OrderItem>()
		.GetAll()
		.Where(ci => productIds.Contains(ci.ProductId))
		.Include(ci => ci.Product) 
		.ToListAsync();

			foreach (var item in orderItems)
			{
				
				
					item.UnitPrice = item.Product.Price;
					item.ModifiedAt = DateTime.UtcNow;
				
			}

			_unitOfWork.Repository<OrderItem>().UpdateList(orderItems);
			await _unitOfWork.CommitAsync();

			_ = _cacheManager.RemoveByTagAsync(CACHE_TAG_ORDER);
		}

	}
} 