using DomainLayer.Enums;
using ApplicationLayer.DtoModels.OrderDtos;
using ApplicationLayer.Services.Cache;
using ApplicationLayer.Services.EmailServices;
using Hangfire;

namespace ApplicationLayer.Services.OrderService
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

        private static string GetOrderFilterKey(string? userId, bool? deleted, int page, int pageSize, OrderStatus? status,bool IsaAdmin=false)
            => $"order:filter:user:{userId ?? "all"}_deleted:{deleted?.ToString() ?? "all"}_page:{page}_size:{pageSize}_status:{status?.ToString() ?? "all"}_IsAdmin:{IsaAdmin}";

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

        public  void SetOrderByIdCacheAsync(int orderId, string userId, bool isAdmin, OrderDto order, TimeSpan? expiration = null)
        {
            var cacheKey = GetOrderByIdKey(orderId, userId, isAdmin);
            _jobClient.Enqueue(()=> _cacheManager.SetAsync(cacheKey, order, expiration ?? TimeSpan.FromMinutes(30), _orderTags));
        }

        public async Task<OrderDto?> GetOrderByIdCacheAsync(int orderId, string userId, bool isAdmin)
        {
            var cacheKey = GetOrderByIdKey(orderId, userId, isAdmin);
            return await _cacheManager.GetAsync<OrderDto>(cacheKey);
        }

        public   void SetOrderByNumberCacheAsync(string orderNumber, string userId, bool isAdmin, OrderDto order, TimeSpan? expiration = null)
        {
            var cacheKey = GetOrderByNumberKey(orderNumber, userId, isAdmin);
            _jobClient.Enqueue(()=> _cacheManager.SetAsync(cacheKey, order, expiration ?? TimeSpan.FromMinutes(30), _orderTags));
        }

        public async Task<OrderDto?> GetOrderByNumberCacheAsync(string orderNumber, string userId, bool isAdmin)
        {
            var cacheKey = GetOrderByNumberKey(orderNumber, userId, isAdmin);
            return await _cacheManager.GetAsync<OrderDto>(cacheKey);
        }

        public   void SetOrderCountCacheAsync(string userId, int? count, TimeSpan? expiration = null)
        {
            var cacheKey = GetOrderCountKey(userId);
            _jobClient.Enqueue(()=> _cacheManager.SetAsync(cacheKey, count, expiration ?? TimeSpan.FromMinutes(15), _orderTags));
        }

        public async Task<int?> GetOrderCountCacheAsync(string userId)
        {
            var cacheKey = GetOrderCountKey(userId);
            return await _cacheManager.GetAsync<int?>(cacheKey);
        }

        public void  SetOrderRevenueCacheAsync(string userId, decimal revenue, TimeSpan? expiration = null)
        {
            var cacheKey = GetOrderRevenueKey(userId);
            _jobClient.Enqueue(() => _cacheManager.SetAsync(cacheKey, revenue, expiration ?? TimeSpan.FromMinutes(20), _orderTags));
        }

        public async Task<decimal?> GetOrderRevenueCacheAsync(string userId)
        {
            var cacheKey = GetOrderRevenueKey(userId);
            return await _cacheManager.GetAsync<decimal?>(cacheKey);
        }

        public  void SetOrderFilterCacheAsync(string? userId, bool? deleted, int page, int pageSize, OrderStatus? status, List<OrderListDto> orders, bool IsAdmin=false,TimeSpan? expiration = null)
        {
            var cacheKey = GetOrderFilterKey(userId, deleted, page, pageSize, status,IsAdmin);
            _jobClient.Enqueue(() => _cacheManager.SetAsync(cacheKey, orders, expiration ?? TimeSpan.FromMinutes(30), _orderTags));
        }

        public async Task<List<OrderListDto>?> GetOrderFilterCacheAsync(string? userId, bool? deleted, int page, int pageSize,bool IsAdmin=false, OrderStatus? status=null)
        {
            var cacheKey = GetOrderFilterKey(userId, deleted, page, pageSize, status);
            return await _cacheManager.GetAsync<List<OrderListDto>>(cacheKey);
        }
    }
}


