using E_Commerce.DtoModels.SubCategorydto;
using E_Commerce.Interfaces;
using E_Commerce.Models;
using E_Commerce.Services;
using E_Commerce.Services.Cache;
using E_Commerce.Services.CategoryServices;
using E_Commerce.Services.EmailServices; // Added for IErrorNotificationService
using Hangfire;

namespace E_Commerce.Services.SubCategoryServices
{
    public class SubCategoryCacheHelper : ISubCategoryCacheHelper
    {
        private readonly ICacheManager _cacheManager;
        private readonly IBackgroundJobClient _backgroundJobClient;
        private readonly string[] _subCategoryTags = new[] { "subcategory", "subcategorywithdata" };
        private const string CACHEWITHDATA = "subcategorywithdata";
        private const string CACHELIST = "subcategorylist";

		private static string GetSubCategoryListKey(string? search,bool? isActive, bool? isDeleted)
            => $"subcategory:list_active:{isActive}_deleted:{isDeleted}_Search:{search}";
        

        private static string GetSubCategoryByIdKey(int id, bool? isActive, bool? isDeleted)
            => $"subcategory:{id}_active:{isActive}_deleted:{isDeleted}";
        
        public SubCategoryCacheHelper(ICacheManager cacheManager, IBackgroundJobClient backgroundJobClient)
        {
            _cacheManager = cacheManager;
            _backgroundJobClient = backgroundJobClient;
        }

		public void ClearSubCategoryDataCache()
		{
			_backgroundJobClient.Enqueue(() => _cacheManager.RemoveByTagAsync(CACHEWITHDATA));
		}
		public void ClearSubCategoryListCache()
        {
            _backgroundJobClient.Enqueue(() => _cacheManager.RemoveByTagAsync(CACHELIST));
		}


		public void ClearSubCategoryCache()
        {
            _backgroundJobClient.Enqueue(() => _cacheManager.RemoveByTagsAsync(_subCategoryTags)); 
        }

        public void NotifyAdminError(string errorMessage, string? stackTrace = null) 
        {
            _backgroundJobClient.Enqueue<IErrorNotificationService>(x => x.SendErrorNotificationAsync(errorMessage, stackTrace)); 
        }

        public async Task SetSubCategoryListCacheAsync(object data,string? search, bool? isActive, bool? isDeleted, TimeSpan? expiration = null)
        {
            var cacheKey = GetSubCategoryListKey(search,isActive, isDeleted);
            await _cacheManager.SetAsync(cacheKey, data, expiration ?? TimeSpan.FromMinutes(30), _subCategoryTags);
        }

        public async Task<T?> GetSubCategoryListCacheAsync<T>(string? search,bool? isActive, bool? isDeleted)
        {
            var cacheKey = GetSubCategoryListKey(search,isActive, isDeleted);
            return await _cacheManager.GetAsync<T>(cacheKey);
        }

        public async Task SetSubCategoryByIdCacheAsync(int id, bool? isActive, bool? isDeleted, object data, TimeSpan? expiration = null)
        {
            var cacheKey = GetSubCategoryByIdKey(id, isActive, isDeleted);
            await _cacheManager.SetAsync(cacheKey, data, expiration ?? TimeSpan.FromMinutes(30), _subCategoryTags);
        }

        public async Task<T?> GetSubCategoryByIdCacheAsync<T>(int id, bool? isActive, bool? isDeleted)
        {
            var cacheKey = GetSubCategoryByIdKey(id, isActive, isDeleted);
            return await _cacheManager.GetAsync<T>(cacheKey);
        }
    }
}