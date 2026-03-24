using ApplicationLayer.DtoModels.CategoryDtos;
using ApplicationLayer.DtoModels.SubCategorydto;
using DomainLayer.Models;
using ApplicationLayer.Services.EmailServices;

namespace ApplicationLayer.Services.SubCategoryServices
{
    public interface ISubCategoryCacheHelper
    {
        void ClearSubCategoryCache();
        void NotifyAdminError(string errorMessage, string? stackTrace = null);
        void SetSubCategoryListCacheAsync<T>(List<T> data, string? search, bool? isActive, bool? isDeleted, int page, int pageSize, bool IsAdmin = false, TimeSpan? expiration = null);
        Task<List<T>?> GetSubCategoryListCacheAsync<T>(string? search, bool? isActive, bool? isDeleted, int page, int pageSize, bool IsAdmin = false);
        public void ClearSubCategoryListCache();
        public void ClearSubCategoryDataCache();

        void SetSubCategoryByIdCacheAsync<T>(int id, bool? isActive, bool? isDeleted, T data, bool IsAdmin = false, TimeSpan? expiration = null);
        Task<T?> GetSubCategoryByIdCacheAsync<T>(int id, bool? isActive, bool? isDeleted, bool IsAdmin = false);
    }
}

