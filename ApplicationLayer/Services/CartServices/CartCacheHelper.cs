using ApplicationLayer.Services.Cache;
using ApplicationLayer.Services.EmailServices;
using Hangfire;

namespace ApplicationLayer.Services.CartServices
{
    public class CartCacheHelper : ICartCacheHelper
    {
        private readonly IBackgroundJobClient _jobClient;
        private readonly ICacheManager _cacheManager;
        private const string CACHE_TAG_CART = "cart";

        private static string GetCartKey(string userId) => $"cart:user:{userId}";

        public CartCacheHelper(IBackgroundJobClient jobClient, ICacheManager cacheManager)
        {
            _jobClient = jobClient;
            _cacheManager = cacheManager;
        }

        public void ClearCartCache()
        {
            _jobClient.Enqueue(() => _cacheManager.RemoveByTagAsync(CACHE_TAG_CART ));
        }

        public void NotifyAdminError(string message, string? stackTrace = null)
        {
            _jobClient.Enqueue<IErrorNotificationService>(_ => _.SendErrorNotificationAsync(message, stackTrace));
        }

        public async Task SetCartCacheAsync(string userId, object data, TimeSpan? expiration = null)
        {
            var cacheKey = GetCartKey(userId);
            await _cacheManager.SetAsync(cacheKey, data, expiration ?? TimeSpan.FromMinutes(30), new string[] { CACHE_TAG_CART });
        }

        public async Task<T?> GetCartCacheAsync<T>(string userId)
        {
            var cacheKey = GetCartKey(userId);
            return await _cacheManager.GetAsync<T>(cacheKey);
        }

        public async Task RemoveCartCacheAsync(string userId)
        {
            var cacheKey = GetCartKey(userId);
            await _cacheManager.RemoveAsync(cacheKey);
        }
    }
}


