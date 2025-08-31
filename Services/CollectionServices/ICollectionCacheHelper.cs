namespace E_Commerce.Services.Collection
{
    public interface ICollectionCacheHelper
    {
        void ClearCollectionCache();
        public void ClearCollectionListCache();
        public void ClearCollectionDataCache();
        void NotifyAdminError(string message, string? stackTrace = null);
        Task SetCollectionListCacheAsync(object data, string? search, bool? isActive, bool? isDeleted, TimeSpan? expiration = null);
        Task<T?> GetCollectionListCacheAsync<T>(string? search, bool? isActive, bool? isDeleted);
        Task SetCollectionByIdCacheAsync(int id, bool? isActive, bool? isDeleted, object data, TimeSpan? expiration = null);
        Task<T?> GetCollectionByIdCacheAsync<T>(int id, bool? isActive, bool? isDeleted);
    }
}
