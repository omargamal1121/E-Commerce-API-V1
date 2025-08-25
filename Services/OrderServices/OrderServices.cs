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
using E_Commerce.Services.ProductServices;

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
		private readonly IBackgroundJobClient _backgroundJobClient;
		private readonly IErrorNotificationService _errorNotificationService;
		private readonly IAdminOpreationServices _adminOperationServices;
		private readonly IProductCatalogService _productCatalogService;
		private readonly ICacheManager _cacheManager;
		private readonly UserManager<Customer> _userManager;
		private const string CACHE_TAG_ORDER = "order";
		private const string CACHE_TAG_CART = "cart";
		private const string PRODUCT_WITH_VARIANT_TAG = "productwithvariantdata";
		private const string CACHE_TAG_PRODUCT_SEARCH = "product_search";
		private const string VARIANT_DATA_TAG = "variantdata";
		private static readonly string[] PRODUCT_CACHE_TAGS = new[] { PRODUCT_WITH_VARIANT_TAG, CACHE_TAG_PRODUCT_SEARCH, VARIANT_DATA_TAG };
		public OrderServices(
			IProductCatalogService productCatalogService,
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
			
			_productCatalogService= productCatalogService;
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
			_backgroundJobClient.Enqueue<IErrorNotificationService>(_ => _.SendErrorNotificationAsync(message, stackTrace));
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
					return Result<OrderWithPaymentDto>.Fail("Address doesn't exist", 400);

				var cartResult = await _cartServices.GetCartAsync(userId);
				if (!cartResult.Success || cartResult.Data == null || cartResult.Data.IsEmpty)
					return Result<OrderWithPaymentDto>.Fail("Cart is empty", 400);

				var cart = cartResult.Data;

				if (cart.CheckoutDate == null || cart.CheckoutDate.Value.AddDays(7) < DateTime.UtcNow)
					return Result<OrderWithPaymentDto>.Fail("Please checkout before creating order", 400);
				#endregion

				#region Check and Reserve Stock
				var variantIds = cart.Items
					.Where(i => i.Product.IsActive && i.DeletedAt == null)
					.Select(i => i.Product.productVariantForCartDto.Id)
					.ToList();

				var variants = await _unitOfWork.Repository<ProductVariant>()
					.GetAll()
					.Where(v => variantIds.Contains(v.Id))
					.ToListAsync();
				var productsids=new List<int>();
				
				foreach (var item in cart.Items)
				{
					var variant = variants.FirstOrDefault(v => v.Id == item.Product.productVariantForCartDto.Id);
					if (variant == null || variant.Quantity < item.Quantity)
					{
						await transaction.RollbackAsync();
						return Result<OrderWithPaymentDto>.Fail(
							$"Product {item.Product.Name} is not available in required quantity",
							400
						);
					}
					productsids.Add(item.ProductId);
					variant.Quantity -= item.Quantity;
					_backgroundJobClient.Enqueue(() => _productCatalogService.UpdateProductQuantity(variant.ProductId));
				}

				_unitOfWork.ProductVariant.UpdateList(variants);
				_backgroundJobClient.Enqueue(() => _cacheManager.RemoveByTagsAsync(PRODUCT_CACHE_TAGS));
				#endregion

				#region Create Order + Items
				var subtotal = cart.Items.Sum(i => i.Quantity * i.UnitPrice);
				var total = subtotal;

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
					CreatedAt = DateTime.UtcNow
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
				}).ToList();

				await _unitOfWork.Repository<OrderItem>().CreateRangeAsync(orderItems.ToArray());
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

				var response = new OrderWithPaymentDto { Order = mappedOrderDto };

				
				_backgroundJobClient.Schedule(
					() => ExpireUnpaidOrderInBackground(createdOrder.Id),
					TimeSpan.FromHours(2)
				);

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
		private async Task ReduceQuantityOfProduct(List<OrderItem> orderItems)
{
    var variantIds = orderItems.Select(o => o.ProductVariantId).ToList();

    var variants = await _unitOfWork.Repository<ProductVariant>()
        .GetAll()
        .Where(v => variantIds.Contains(v.Id))
        .ToListAsync();

		foreach (var item in variants)
		{
			var orderItem = orderItems.First(o => o.ProductVariantId == item.Id);
			item.Quantity -= orderItem.Quantity;
				_backgroundJobClient.Enqueue(() => _productCatalogService.UpdateProductQuantity(item.ProductId));
		}

		    _unitOfWork.ProductVariant.UpdateList(variants);
			_backgroundJobClient.Enqueue(()=> _cacheManager.RemoveByTagsAsync(PRODUCT_CACHE_TAGS));
		}




		public async Task<Result<bool>> UpdateOrderAfterPaid(int orderId, OrderStatus orderStatus)
		{
			await using var transaction = await _unitOfWork.BeginTransactionAsync();
			try
			{
				var order = await _orderRepository.GetAll().Include(o=>o.Items).Where(o=>o.Id==orderId).FirstOrDefaultAsync();
				if (order == null)
					return Result<bool>.Fail("Can't find order", 404);

			
				if (orderStatus == OrderStatus.Processing && order.RestockedAt != null)
				{
					_backgroundJobClient.Enqueue(() => ReduceQuantityOfProduct(order.Items.ToList()));
					order.RestockedAt = null;
				}

				order.Status = orderStatus;

				await _unitOfWork.CommitAsync();
				await transaction.CommitAsync();

				BackgroundJob.Enqueue(() => InvalidateCacheInBackground());

				_logger.LogInformation("Updated order {OrderId} to {Status} after payment", orderId, order.Status);
				return Result<bool>.Ok(true, "Order updated after payment", 200);
			}
			catch (Exception ex)
			{
				await transaction.RollbackAsync();
				_logger.LogError(ex, "Error updating order {OrderId} after payment", orderId);
				NotifyAdminOfError($"Error updating order {orderId} after payment: {ex.Message}", ex.StackTrace);
				return Result<bool>.Fail("An error occurred while updating the order after payment", 500);
			}
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
			DeletedAt = order.DeletedAt,
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

				if (order.Status != OrderStatus.PendingPayment)
				{
					_logger.LogInformation("Expire skipped: order {OrderId} status is {Status}", orderId, order.Status);
					return;
				}
				order.Status = OrderStatus.PaymentExpired;
				await _unitOfWork.CommitAsync();
				await transaction.CommitAsync();
				BackgroundJob.Enqueue(() => RestockOrderItemsInBackground(orderId));
				BackgroundJob.Enqueue(() => InvalidateCacheInBackground());
				BackgroundJob.Enqueue(() => _cacheManager.RemoveByTagsAsync(PRODUCT_CACHE_TAGS));
				_logger.LogInformation("Order {OrderId} auto-expired and restock scheduled", orderId);
			}
			catch (Exception ex)
			{
				await transaction.RollbackAsync();
				_logger.LogError(ex, "Error while auto-expiring order {OrderId}", orderId);
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
				_logger.LogError($"Error caching revenue with key {cacheKey}: {ex.Message}");
			}
		}

		public async Task RestockOrderItemsInBackground(int orderId)
		{
			await using var transaction = await _unitOfWork.BeginTransactionAsync();

			try
			{
				var order = await _unitOfWork.Repository<E_Commerce.Models.Order>().GetByIdAsync(orderId);
				if (order == null)
				{
					_logger.LogWarning("Restock skipped: order {OrderId} not found", orderId);
					return;
				}

				if (order.RestockedAt.HasValue)
				{
					_logger.LogInformation("Restock skipped: order {OrderId} already restocked at {RestockedAt}", orderId, order.RestockedAt);
					return;
				}

				if (order.Status == OrderStatus.PendingPayment &&
					order.CreatedAt.HasValue &&
					order.CreatedAt.Value.AddHours(2) < DateTime.UtcNow)
				{
					order.Status = OrderStatus.PaymentExpired;
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

				var variantIds = orderItems.Select(i => i.ProductVariantId).ToList();
				var variants = await _unitOfWork.Repository<ProductVariant>()
					.GetAll()
					.Where(v => variantIds.Contains(v.Id))
					.ToListAsync();

				foreach (var item in orderItems)
				{
					var variant = variants.FirstOrDefault(v => v.Id == item.ProductVariantId);
					if (variant != null)
					{
						variant.Quantity += item.Quantity;
				_backgroundJobClient.Enqueue(()=> _productCatalogService.UpdateProductQuantity(variant.ProductId));
					}
				}

				_unitOfWork.ProductVariant.UpdateList(variants);

				// Mark restocked
				order.RestockedAt = DateTime.UtcNow;

				await _unitOfWork.CommitAsync();
				await transaction.CommitAsync();

				// Invalidate product search cache
				_backgroundJobClient.Enqueue(()=> _cacheManager.RemoveByTagsAsync(PRODUCT_CACHE_TAGS));

				_logger.LogInformation("Restocked inventory for order {OrderId}", orderId);
			}
			catch (Exception ex)
			{
				await transaction.RollbackAsync();
				_logger.LogError(ex, "Error while restocking inventory for order {OrderId}", orderId);

				_backgroundJobClient.Enqueue(() =>
					_errorNotificationService.SendErrorNotificationAsync(ex.Message, null));
			}
		}



		private bool IsValidTransition(OrderStatus current, OrderStatus target)
		{
			return current switch
			{
				OrderStatus.PendingPayment => target is OrderStatus.Confirmed or OrderStatus.PaymentExpired or OrderStatus.CancelledByUser or OrderStatus.CancelledByAdmin,
				OrderStatus.Confirmed => target is OrderStatus.Processing or OrderStatus.CancelledByAdmin,
				OrderStatus.Processing => target is OrderStatus.Shipped or OrderStatus.CancelledByAdmin,
				OrderStatus.Shipped => target is OrderStatus.Delivered or OrderStatus.Returned,
				OrderStatus.Delivered => target is OrderStatus.Complete or OrderStatus.Returned or OrderStatus.Refunded,
				OrderStatus.PaymentExpired => target is OrderStatus.PendingPayment or OrderStatus.CancelledByAdmin or OrderStatus.CancelledByUser,
				_ => false
			};
		}
		private async Task<Result<bool>> UpdateStatusAsync(
	int orderId,
	string adminId,
	OrderStatus target,
	string operationTitle,
	string successMessage,
	string? notes = null)
		{
			await using var transaction = await _unitOfWork.BeginTransactionAsync();
			try
			{
				var order = await _orderRepository.GetByIdAsync(orderId);
				if (order == null)
					return Result<bool>.Fail("Order not found", 404);

				if (!IsValidTransition(order.Status, target))
					return Result<bool>.Fail("Invalid status transition", 400);
				order.Status = target;
				if (target == OrderStatus.Shipped)
					order.ShippedAt = DateTime.Now;

				if (target == OrderStatus.Delivered)
					order.DeliveredAt = DateTime.Now;


				var log = await _adminOperationServices.AddAdminOpreationAsync(
					$"{operationTitle} order {orderId}",
					Opreations.UpdateOpreation,
					adminId,
					orderId);

				if (!log.Success)
					_logger.LogWarning("Admin log failed while {Op} order {OrderId}: {Msg}", operationTitle, orderId, log.Message);

				await _unitOfWork.CommitAsync();
				await transaction.CommitAsync();
				await _cacheManager.RemoveByTagAsync(CACHE_TAG_ORDER);

				return Result<bool>.Ok(true, successMessage, 200);
			}
			catch (Exception ex)
			{
				await transaction.RollbackAsync();
				_logger.LogError(ex, "Error updating order {OrderId} to {Target}", orderId, target);
				return Result<bool>.Fail("An error occurred while updating order status", 500);
			}
		}

		public Task<Result<bool>> ConfirmOrderAsync(int orderId, string adminId, string? notes = null)
	=> UpdateStatusAsync(orderId, adminId, OrderStatus.Confirmed, "Confirmed", "Order confirmed", notes);

		public Task<Result<bool>> ProcessOrderAsync(int orderId, string adminId, string? notes = null)
			=> UpdateStatusAsync(orderId, adminId, OrderStatus.Processing, "Processing", "Order set to processing", notes);

		public Task<Result<bool>> RefundOrderAsync(int orderId, string adminId, string? notes = null)
			=> UpdateStatusAsync(orderId, adminId, OrderStatus.Refunded, "Refunded", "Order refunded", notes);

		public Task<Result<bool>> ReturnOrderAsync(int orderId, string adminId, string? notes = null)
			=> UpdateStatusAsync(orderId, adminId, OrderStatus.Returned, "Returned", "Order marked as returned", notes);

		public Task<Result<bool>> ExpirePaymentAsync(int orderId, string adminId, string? notes = null)
			=> UpdateStatusAsync(orderId, adminId, OrderStatus.PaymentExpired, "Payment expired", "Order payment expired", notes);

		public Task<Result<bool>> CompleteOrderAsync(int orderId, string adminId, string? notes = null)
			=> UpdateStatusAsync(orderId, adminId, OrderStatus.Complete, "Completed", "Order completed", notes);

		public Task<Result<bool>> ShipOrderAsync(int orderId, string adminId, string? notes = null)
			=> UpdateStatusAsync(orderId, adminId, OrderStatus.Shipped, "Shipped", "Order shipped successfully", notes);

		public Task<Result<bool>> DeliverOrderAsync(int orderId, string adminId, string? notes = null)
			=> UpdateStatusAsync(orderId, adminId, OrderStatus.Delivered, "Delivered", "Order delivered successfully", notes);

		public Task<Result<bool>> ShipOrderAsync(int orderId, string userId)
			=> UpdateStatusAsync(orderId, userId, OrderStatus.Shipped, "Shipped", "Order shipped successfully", null);

		public Task<Result<bool>> DeliverOrderAsync(int orderId, string userId)
			=> UpdateStatusAsync(orderId, userId, OrderStatus.Delivered, "Delivered", "Order delivered successfully", null);


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
			await using var transaction = await _unitOfWork.BeginTransactionAsync();
			try
			{
				var order = await _orderRepository.GetByIdAsync(orderId);
				if (order == null || order.CustomerId != userId)
				{
					await transaction.RollbackAsync();
					return Result<bool>.Fail("Order not found or access denied", 404);
				}

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

				BackgroundJob.Enqueue(() => InvalidateCacheInBackground());
				BackgroundJob.Enqueue(() => RestockOrderItemsInBackground(orderId));

				return Result<bool>.Ok(true, "Order cancelled successfully", 200);
			}
			catch (Exception ex)
			{
				await transaction.RollbackAsync();
				_logger.LogError(ex, "Error cancelling order {OrderId} by user {UserId}", orderId, userId);
				NotifyAdminOfError($"Error cancelling order {orderId} by user {userId}: {ex.Message}", ex.StackTrace);
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
				BackgroundJob.Enqueue(() => InvalidateCacheInBackground());
				BackgroundJob.Enqueue(() => RestockOrderItemsInBackground(orderId));

				return Result<bool>.Ok(true, "Order cancelled by admin", 200);
			}
			catch (Exception ex)
			{
				await transaction.RollbackAsync();
				_logger.LogError(ex, "Error while cancelling order by admin {AdminId}", adminId);
				NotifyAdminOfError(ex.Message, ex.StackTrace);
				return Result<bool>.Fail("An error occurred while cancelling order", 500);
			}
		}



		public async Task<Result<decimal>> GetTotalRevenueByDateRangeAsync(DateTime startDate, DateTime endDate)
		{


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


		public async Task<Result<int?>> GetTotalOrderCountAsync(OrderStatus? status)
		{

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
			_logger.LogInformation(
				"Filtering orders - UserId: {UserId}, Deleted: {Deleted}, Page: {Page}, PageSize: {PageSize}, Status: {Status}",
				userId, deleted, page, pageSize, status);

			var cacheKey = $"{CACHE_TAG_ORDER}_filter_user_{userId ?? "all"}_deleted_{deleted?.ToString() ?? "all"}_page_{page}_size_{pageSize}_status_{status?.ToString() ?? "all"}";
			var cached = await _cacheManager.GetAsync<List<OrderListDto>>(cacheKey);
			if (cached != null)
			{
				_logger.LogInformation("Cache hit for filtered orders with key: {CacheKey}", cacheKey);
				return Result<List<OrderListDto>>.Ok(cached, "Filtered orders retrieved from cache", 200);
			}

			try
			{
				var query = _unitOfWork.Repository<E_Commerce.Models.Order>()
					.GetAll();

				if (!string.IsNullOrEmpty(userId))
					query = query.Where(o => o.CustomerId == userId);

				if (deleted.HasValue)
				{
					if (deleted.Value)
						query = query.Where(o => o.DeletedAt != null);
					else
						query = query.Where(o => o.DeletedAt == null);
				}

				if (status.HasValue)
					query = query.Where(o => o.Status == status.Value);

				var orders = await query
					.OrderByDescending(o => o.CreatedAt)
					.Select(OrderListSelector)
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
				_logger.LogError(ex, "Error filtering orders");
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

		
		

	}
}