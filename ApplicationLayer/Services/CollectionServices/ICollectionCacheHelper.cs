namespace Application.Services.CollectionServices
{
    public interface ICollectionCacheHelper
    {
        Task ClearCollectionCache();
        public Task ClearCollectionListCache();
        public Task ClearCollectionDataCache();
        void NotifyAdminError(string message, string? stackTrace = null);
        void SetCollectionListCacheAsync<T>(List<T> data, string? search, bool? isActive, bool? isDeleted, int page, int pageSize, bool IsAdmin = false, TimeSpan? expiration = null);
        Task<List<T>?> GetCollectionListCacheAsync<T>(string? search, bool? isActive, bool? isDeleted, int page, int pageSize, bool IsAdmin = false);
        void SetCollectionByIdCacheAsync(int id, bool? isActive, bool? isDeleted, object data, bool IsAdmin = false, TimeSpan? expiration = null);
        Task<T?> GetCollectionByIdCacheAsync<T>(int id, bool? isActive, bool? isDeleted, bool IsAdmin = false);
    }
}


