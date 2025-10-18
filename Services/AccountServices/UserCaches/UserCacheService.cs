using E_Commerce.Services.Cache;
using StackExchange.Redis;

namespace E_Commerce.Services.AccountServices.UserCaches
{
    public class UserCacheService: IUserCacheService
    {
        private readonly ICacheManager _cache;
        private readonly ILogger<UserCacheService> _logger;

        public UserCacheService(ICacheManager cache, ILogger<UserCacheService> logger)
        {
            _cache = cache;
            _logger = logger;
        }

        private string GetKey(string userId) => $"security_user:{userId}";
        public async Task SetAsync<T>(string userId, T data, TimeSpan? expiry = null)
        {
            await _cache.SetAsync(GetKey(userId), data, expiry);
        }

        public async Task DeleteByUserIdAsync(string userId)
        {
            var key = GetKey(userId);
            await _cache.RemoveAsync(key);
        }
        public async Task<T?> GetAsync<T>(string userId)
        {
            var key = GetKey(userId);
           var value= await _cache.GetAsync<T>(key);

            return value;
        }
    }
}
