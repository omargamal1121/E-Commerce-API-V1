using DomainLayer.Enums;
using ApplicationLayer.DtoModels.OrderDtos;
using ApplicationLayer.Interfaces;

using Hangfire;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ApplicationLayer.Services.OrderService
{
    public class OrderQueryService : IOrderQueryService
    {
        private readonly ILogger<OrderQueryService> _logger;
        private readonly IUnitOfWork _unitOfWork;
        private readonly IOrderRepository _orderRepository;
        private readonly IOrderCacheHelper _cacheHelper;
        private readonly IOrderMapper _mapper;

        public OrderQueryService(
            ILogger<OrderQueryService> logger,
            IUnitOfWork unitOfWork,
            IOrderRepository orderRepository,
            IOrderCacheHelper cacheHelper,
            IOrderMapper mapper)
        {
            _logger = logger;
            _unitOfWork = unitOfWork;
            _orderRepository = orderRepository;
            _cacheHelper = cacheHelper;
            _mapper = mapper;
        }

        public async Task<Result<OrderDto>> GetOrderByIdAsync(int orderId, string userId, bool isAdmin = false)
        {
            _logger.LogInformation("Getting order by ID: {OrderId} for user: {UserId}, IsAdmin: {IsAdmin}", orderId, userId, isAdmin);

            var cached = await _cacheHelper.GetOrderByIdCacheAsync(orderId, userId, isAdmin);
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

                var order = await _unitOfWork.Repository<DomainLayer.Models.Order>()
                    .GetAll()
                    .Where(o => o.Id == orderId)
                    .Select(_mapper.OrderSelector)
                    .FirstOrDefaultAsync();

                if (order == null)
                {
                    _logger.LogWarning("Order {OrderId} not found after confirmed existence (unexpected)", orderId);
                    return Result<OrderDto>.Fail("Order not found", 404);
                }

                BackgroundJob.Enqueue(() => _cacheHelper.SetOrderByIdCacheAsync(orderId, userId, isAdmin, order, TimeSpan.FromMinutes(30)));

                _logger.LogInformation("Order {OrderId} retrieved successfully for user {UserId}", orderId, userId);
                return Result<OrderDto>.Ok(order, "Order retrieved successfully", 200);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving order {OrderId} for user {UserId}", orderId, userId);
                _cacheHelper.NotifyAdminError($"Error getting order {orderId}: {ex.Message}", ex.StackTrace);
                return Result<OrderDto>.Fail("Unexpected error while retrieving order", 500);
            }
        }

        public async Task<Result<OrderDto>> GetOrderByNumberAsync(string orderNumber, string userId, bool isAdmin = false)
        {
            _logger.LogInformation("Getting order by number: {OrderNumber} for user: {UserId}, IsAdmin: {IsAdmin}", orderNumber, userId, isAdmin);

            var cached = await _cacheHelper.GetOrderByNumberCacheAsync(orderNumber, userId, isAdmin);
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

                var order = await _unitOfWork.Repository<DomainLayer.Models.Order>()
                    .GetAll()
                    .Where(o => o.OrderNumber == orderNumber)
                    .Select(_mapper.OrderSelector)
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

                BackgroundJob.Enqueue(() => _cacheHelper.SetOrderByNumberCacheAsync(orderNumber, userId, isAdmin, order, TimeSpan.FromMinutes(30)));

                _logger.LogInformation("Order {OrderNumber} retrieved successfully for user {UserId}", orderNumber, userId);
                return Result<OrderDto>.Ok(order, "Order retrieved successfully", 200);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting order by number {OrderNumber} for user {UserId}", orderNumber, userId);
                _cacheHelper.NotifyAdminError($"Error getting order by number {orderNumber}: {ex.Message}", ex.StackTrace);
                return Result<OrderDto>.Fail("Unexpected error while retrieving order", 500);
            }
        }

        public async Task<Result<int?>> GetOrderCountByCustomerAsync(string userId)
        {
            var cached = await _cacheHelper.GetOrderCountCacheAsync(userId);
            if (cached != null)
            {
                _logger.LogInformation($"Cache hit for order count for customer {userId}");
                return Result<int?>.Ok(cached, "Order count retrieved from cache", 200);
            }

            try
            {
                var count = await _orderRepository.GetOrderCountByCustomerAsync(userId);

                BackgroundJob.Enqueue(() => _cacheHelper.SetOrderCountCacheAsync(userId, count, TimeSpan.FromMinutes(15)));

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
            var cached = await _cacheHelper.GetOrderRevenueCacheAsync(userId);
            if (cached != null)
            {
                _logger.LogInformation($"Cache hit for total revenue for customer {userId}");
                return Result<decimal>.Ok(cached.Value, "Total revenue retrieved from cache", 200);
            }

            try
            {
                var revenue = await _orderRepository.GetTotalRevenueByCustomerAsync(userId);

                BackgroundJob.Enqueue(() => _cacheHelper.SetOrderRevenueCacheAsync(userId, revenue, TimeSpan.FromMinutes(20)));

                return Result<decimal>.Ok(revenue, "Total revenue retrieved", 200);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error getting total revenue for user {userId}: {ex.Message}");
                return Result<decimal>.Fail("An error occurred while getting total revenue", 500);
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
            OrderStatus? status = null,
            bool IsAdmin=false)
        {
            _logger.LogInformation(
                "Filtering orders - UserId: {UserId}, Deleted: {Deleted}, Page: {Page}, PageSize: {PageSize}, Status: {Status}, IsAdmin: {IsAdmin}",
                userId, deleted, page, pageSize, status,IsAdmin);

            var cached = await _cacheHelper.GetOrderFilterCacheAsync(userId, deleted, page, pageSize, IsAdmin,status);
            if (cached != null)
            {
                _logger.LogInformation("Cache hit for filtered orders");
                return Result<List<OrderListDto>>.Ok(cached, "Filtered orders retrieved from cache", 200);
            }

            try
            {
                var query = _unitOfWork.Repository<DomainLayer.Models.Order>()
                    .GetAll();

                if(!IsAdmin&& string.IsNullOrEmpty(userId))
                      return Result<List<OrderListDto>>.Ok(new List<OrderListDto>(), "No orders found matching the criteria", 200);

                if (!string.IsNullOrEmpty(userId))
                    query = query.Where(o => o.CustomerId == userId);

                if (deleted.HasValue&&IsAdmin)
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
                    .Select(_mapper.OrderListSelector)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToListAsync();

                if (!orders.Any())
                {
                    return Result<List<OrderListDto>>.Ok(new List<OrderListDto>(), "No orders found matching the criteria", 200);
                }

                BackgroundJob.Enqueue(() => _cacheHelper.SetOrderFilterCacheAsync(userId, deleted, page, pageSize, status, orders, IsAdmin,TimeSpan.FromMinutes(30)));

                return Result<List<OrderListDto>>.Ok(orders, "Filtered orders retrieved successfully", 200);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error filtering orders");
                _cacheHelper.NotifyAdminError($"Error filtering orders: {ex.Message}", ex.StackTrace);
                return Result<List<OrderListDto>>.Fail("An error occurred while filtering orders", 500);
            }
        }
    }
}


