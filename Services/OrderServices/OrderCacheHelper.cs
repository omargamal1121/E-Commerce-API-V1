using E_Commerce.DtoModels.OrderDtos;
using E_Commerce.Enums;
using E_Commerce.Services.Cache;
using E_Commerce.Services.EmailServices;
using Hangfire;

namespace E_Commerce.Services.Order
{
    public class OrderCacheHelper : IOrderCacheHelper
    {
        private readonly IBackgroundJobClient _jobClient;
        private readonly ICacheManager _cacheManager;
        private readonly string[] _orderTags = new[] { "order" };
        private const string CACHE_ORDER = "order";

        private static string GetOrderByIdKey(int orderId, string userId, bool isAdmin)
            => $"order:id:{orderId}_user:{userId}_admin:{isAdmin}";

        private static string GetOrderByNumberKey(string orderNumber, string userId, bool isAdmin)
            => $"order:number:{orderNumber}_user:{userId}_admin:{isAdmin}";

        private static string GetOrderCountKey(string userId)
            => $"order:count:customer:{userId}";

        private static string GetOrderRevenueKey(string userId)
            => $"order:revenue:customer:{userId}";

        private static string GetOrderFilterKey(string? userId, bool? deleted, int page, int pageSize, OrderStatus? status)
            => $"order:filter:user:{userId ?? "all"}_deleted:{deleted?.ToString() ?? "all"}_page:{page}_size:{pageSize}_status:{status?.ToString() ?? "all"}";

        public OrderCacheHelper(IBackgroundJobClient jobClient, ICacheManager cacheManager)
        {
            _jobClient = jobClient;
            _cacheManager = cacheManager;
        }

        public void ClearOrderCache()
        {
            _jobClient.Enqueue(() => _cacheManager.RemoveByTagsAsync(_orderTags));
        }

        public void NotifyAdminError(string message, string? stackTrace = null)
        {
            _jobClient.Enqueue<IErrorNotificationService>(_ => _.SendErrorNotificationAsync(message, stackTrace));
        }

        public async Task SetOrderByIdCacheAsync(int orderId, string userId, bool isAdmin, OrderDto order, TimeSpan? expiration = null)
        {
            var cacheKey = GetOrderByIdKey(orderId, userId, isAdmin);
            await _cacheManager.SetAsync(cacheKey, order, expiration ?? TimeSpan.FromMinutes(30), _orderTags);
        }

        public async Task<OrderDto?> GetOrderByIdCacheAsync(int orderId, string userId, bool isAdmin)
        {
            var cacheKey = GetOrderByIdKey(orderId, userId, isAdmin);
            return await _cacheManager.GetAsync<OrderDto>(cacheKey);
        }

        public async Task SetOrderByNumberCacheAsync(string orderNumber, string userId, bool isAdmin, OrderDto order, TimeSpan? expiration = null)
        {
            var cacheKey = GetOrderByNumberKey(orderNumber, userId, isAdmin);
            await _cacheManager.SetAsync(cacheKey, order, expiration ?? TimeSpan.FromMinutes(30), _orderTags);
        }

        public async Task<OrderDto?> GetOrderByNumberCacheAsync(string orderNumber, string userId, bool isAdmin)
        {
            var cacheKey = GetOrderByNumberKey(orderNumber, userId, isAdmin);
            return await _cacheManager.GetAsync<OrderDto>(cacheKey);
        }

        public async Task SetOrderCountCacheAsync(string userId, int? count, TimeSpan? expiration = null)
        {
            var cacheKey = GetOrderCountKey(userId);
            await _cacheManager.SetAsync(cacheKey, count, expiration ?? TimeSpan.FromMinutes(15), _orderTags);
        }

        public async Task<int?> GetOrderCountCacheAsync(string userId)
        {
            var cacheKey = GetOrderCountKey(userId);
            return await _cacheManager.GetAsync<int?>(cacheKey);
        }

        public async Task SetOrderRevenueCacheAsync(string userId, decimal revenue, TimeSpan? expiration = null)
        {
            var cacheKey = GetOrderRevenueKey(userId);
            await _cacheManager.SetAsync(cacheKey, revenue, expiration ?? TimeSpan.FromMinutes(20), _orderTags);
        }

        public async Task<decimal?> GetOrderRevenueCacheAsync(string userId)
        {
            var cacheKey = GetOrderRevenueKey(userId);
            return await _cacheManager.GetAsync<decimal?>(cacheKey);
        }

        public async Task SetOrderFilterCacheAsync(string? userId, bool? deleted, int page, int pageSize, OrderStatus? status, List<OrderListDto> orders, TimeSpan? expiration = null)
        {
            var cacheKey = GetOrderFilterKey(userId, deleted, page, pageSize, status);
            await _cacheManager.SetAsync(cacheKey, orders, expiration ?? TimeSpan.FromMinutes(30), _orderTags);
        }

        public async Task<List<OrderListDto>?> GetOrderFilterCacheAsync(string? userId, bool? deleted, int page, int pageSize, OrderStatus? status)
        {
            var cacheKey = GetOrderFilterKey(userId, deleted, page, pageSize, status);
            return await _cacheManager.GetAsync<List<OrderListDto>>(cacheKey);
        }
    }
}
