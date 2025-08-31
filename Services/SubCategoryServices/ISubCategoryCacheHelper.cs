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
        Task SetSubCategoryListCacheAsync(object data,string? search, bool? isActive, bool? isDeleted, TimeSpan? expiration = null);
        Task<T?> GetSubCategoryListCacheAsync<T>(string? search, bool? isActive, bool? isDeleted);
		public void ClearSubCategoryListCache();
        public void ClearSubCategoryDataCache();

		Task SetSubCategoryByIdCacheAsync(int id, bool? isActive, bool? isDeleted, object data, TimeSpan? expiration = null);
        Task<T?> GetSubCategoryByIdCacheAsync<T>(int id, bool? isActive, bool? isDeleted);
    }
}