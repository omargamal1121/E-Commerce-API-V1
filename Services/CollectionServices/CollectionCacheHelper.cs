using E_Commerce.DtoModels.CollectionDtos;
using E_Commerce.Services.Cache;
using E_Commerce.Services.EmailServices;
using Hangfire;

namespace E_Commerce.Services.Collection
{
    public class CollectionCacheHelper : ICollectionCacheHelper
    {
        private readonly IBackgroundJobClient _jobClient;
        private readonly ICacheManager _cacheManager;
        private readonly string[] _collectionTags = new[] { "collection", "CollectionWithProduct", "collectionlist" };
        private const string CACHEWITHDATA = "CollectionWithProduct";
        private const string CACHELIST = "collectionlist";

        private static string GetCollectionListKey(string? search, bool? isActive, bool? isDeleted, int page, int pageSize, bool IsAdmin = false)
            => $"collection:list_active:{isActive}_deleted:{isDeleted}_search:{search}_page:{page}_pageSize:{pageSize}_IsAdmin:{IsAdmin}";

        private static string GetCollectionByIdKey(int id, bool? isActive, bool? isDeleted, bool IsAdmin = false)
            => $"collection:{id}_active:{isActive}_deleted:{isDeleted}_IsAdmin:{IsAdmin}";

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

        public  void SetCollectionListCacheAsync(List<CollectionSummaryDto> data, string? search, bool? isActive, bool? isDeleted, int page, int pageSize, bool IsAdmin = false, TimeSpan? expiration = null)
        {
            var cacheKey = GetCollectionListKey(search, isActive, isDeleted, page, pageSize, IsAdmin);
            _jobClient.Enqueue(()=> _cacheManager.SetAsync(cacheKey, data, expiration ?? TimeSpan.FromMinutes(30), new string[] { CACHELIST }));
        }

        public async Task<T?> GetCollectionListCacheAsync<T>(string? search, bool? isActive, bool? isDeleted, int page, int pageSize, bool IsAdmin = false)
        {
            var cacheKey = GetCollectionListKey(search, isActive, isDeleted, page, pageSize, IsAdmin);
            return await _cacheManager.GetAsync<T>(cacheKey);
        }

        public  void SetCollectionByIdCacheAsync(int id, bool? isActive, bool? isDeleted, CollectionDto data, bool IsAdmin = false, TimeSpan? expiration = null)
        {
            var cacheKey = GetCollectionByIdKey(id, isActive, isDeleted, IsAdmin);
            _jobClient.Enqueue(()=> _cacheManager.SetAsync(cacheKey, data, expiration ?? TimeSpan.FromMinutes(30), new string[] { CACHEWITHDATA }));
        }

        public async Task<T?> GetCollectionByIdCacheAsync<T>(int id, bool? isActive, bool? isDeleted, bool IsAdmin = false)
        {
            var cacheKey = GetCollectionByIdKey(id, isActive, isDeleted, IsAdmin);
            return await _cacheManager.GetAsync<T>(cacheKey);
        }
    }
}
