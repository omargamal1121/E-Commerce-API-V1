using E_Commerce.DtoModels.CategoryDtos;
using E_Commerce.DtoModels.SubCategorydto;
using E_Commerce.Models;
using E_Commerce.Services.EmailServices;

namespace E_Commerce.Services.SubCategoryServices
{
    public interface ISubCategoryCacheHelper
    {
        void ClearSubCategoryCache();
        void NotifyAdminError(string errorMessage, string? stackTrace = null);
        void SetSubCategoryListCacheAsync(List<SubCategoryDto> data, string? search, bool? isActive, bool? isDeleted, int page, int pageSize, bool IsAdmin = false, TimeSpan? expiration = null);
        Task<T?> GetSubCategoryListCacheAsync<T>(string? search, bool? isActive, bool? isDeleted, int page, int pageSize, bool IsAdmin = false);
        public void ClearSubCategoryListCache();
        public void ClearSubCategoryDataCache();

        void SetSubCategoryByIdCacheAsync(int id, bool? isActive, bool? isDeleted, SubCategoryDtoWithData data, bool IsAdmin = false, TimeSpan? expiration = null);
        Task<T?> GetSubCategoryByIdCacheAsync<T>(int id, bool? isActive, bool? isDeleted, bool IsAdmin = false);
    }
}