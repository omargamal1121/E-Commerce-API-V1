using E_Commerce.DtoModels.CollectionDtos;

namespace E_Commerce.Services.Collection
{
    public interface ICollectionCacheHelper
    {
        void ClearCollectionCache();
        public void ClearCollectionListCache();
        public void ClearCollectionDataCache();
        void NotifyAdminError(string message, string? stackTrace = null);
        void SetCollectionListCacheAsync(List<CollectionSummaryDto> data, string? search, bool? isActive, bool? isDeleted, int page, int pageSize, bool IsAdmin = false, TimeSpan? expiration = null);
        Task<T?> GetCollectionListCacheAsync<T>(string? search, bool? isActive, bool? isDeleted, int page, int pageSize, bool IsAdmin = false);
        void SetCollectionByIdCacheAsync(int id, bool? isActive, bool? isDeleted, CollectionDto data, bool IsAdmin = false, TimeSpan? expiration = null);
        Task<T?> GetCollectionByIdCacheAsync<T>(int id, bool? isActive, bool? isDeleted, bool IsAdmin = false);
    }
}
