using E_Commerce.Services.Cache;
using E_Commerce.Services.EmailServices;
using Hangfire;

namespace E_Commerce.Services.Collection
{
    public class CollectionCacheHelper : ICollectionCacheHelper
    {
        private readonly IBackgroundJobClient _jobClient;
        private readonly ICacheManager _cacheManager;
        private readonly string[] _collectionTags = new[] { "collection", "CollectionWithProduct" };
        private const string CACHEWITHDATA = "CollectionWithProduct";
        private const string CACHELIST = "collection";

        private static string GetCollectionListKey(string? search, bool? isActive, bool? isDeleted)
            => $"collection:list_active:{isActive}_deleted:{isDeleted}_search:{search}";

        private static string GetCollectionByIdKey(int id, bool? isActive, bool? isDeleted)
            => $"collection:{id}_active:{isActive}_deleted:{isDeleted}";

        public CollectionCacheHelper(IBackgroundJobClient jobClient, ICacheManager cacheManager)
        {
            _jobClient = jobClient;
            _cacheManager = cacheManager;
        }

        public void ClearCollectionListCache()
        {
            _jobClient.Enqueue(() => _cacheManager.RemoveByTagsAsync(new string[] { CACHELIST }));
        }

        public void ClearCollectionCache()
        {
            _jobClient.Enqueue(() => _cacheManager.RemoveByTagsAsync(_collectionTags));
        }

        public void ClearCollectionDataCache()
        {
            _jobClient.Enqueue(() => _cacheManager.RemoveByTagAsync(CACHEWITHDATA));
        }

        public void NotifyAdminError(string message, string? stackTrace = null)
        {
            _jobClient.Enqueue<IErrorNotificationService>(_ => _.SendErrorNotificationAsync(message, stackTrace));
        }

        public async Task SetCollectionListCacheAsync(object data, string? search, bool? isActive, bool? isDeleted, TimeSpan? expiration = null)
        {
            var cacheKey = GetCollectionListKey(search, isActive, isDeleted);
            await _cacheManager.SetAsync(cacheKey, data, expiration ?? TimeSpan.FromMinutes(30), new string[] { CACHELIST });
        }

        public async Task<T?> GetCollectionListCacheAsync<T>(string? search, bool? isActive, bool? isDeleted)
        {
            var cacheKey = GetCollectionListKey(search, isActive, isDeleted);
            return await _cacheManager.GetAsync<T>(cacheKey);
        }

        public async Task SetCollectionByIdCacheAsync(int id, bool? isActive, bool? isDeleted, object data, TimeSpan? expiration = null)
        {
            var cacheKey = GetCollectionByIdKey(id, isActive, isDeleted);
            await _cacheManager.SetAsync(cacheKey, data, expiration ?? TimeSpan.FromMinutes(30), new string[] { CACHEWITHDATA });
        }

        public async Task<T?> GetCollectionByIdCacheAsync<T>(int id, bool? isActive, bool? isDeleted)
        {
            var cacheKey = GetCollectionByIdKey(id, isActive, isDeleted);
            return await _cacheManager.GetAsync<T>(cacheKey);
        }
    }
}
