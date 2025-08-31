using E_Commerce.Services.Cache;
using E_Commerce.Services.EmailServices;
using Hangfire;

namespace E_Commerce.Services.CategoryServices
{
	public class CategoryCacheHelper : ICategoryCacheHelper
	{
		private readonly IBackgroundJobClient _jobClient;
		private readonly ICacheManager _cacheManager;
		private readonly string[] _categoryTags = new[] { "category", "categorywithdata" };
		private const string CACHEWITHDATA = "categorywithdata";
		private const string CACHELIST = "categorywithdata";

		private static string GetCategoryListKey(string?search,bool? isActive, bool? isDeleted)
			=> $"category:list_active:{isActive}_deleted:{isDeleted}_search:{search}";

		private static string GetCategoryByIdKey(int id, bool? isActive, bool? isDeleted)
			=> $"category:{id}_active:{isActive}_deleted:{isDeleted}";

		public CategoryCacheHelper(IBackgroundJobClient jobClient, ICacheManager cacheManager)
		{
			_jobClient = jobClient;
			_cacheManager = cacheManager;
		}

		public void ClearCategoryListCache()
		{
			_jobClient.Enqueue(() => _cacheManager.RemoveByTagsAsync(new string[] { CACHELIST  }));
		}
		public void ClearCategoryCache()
		{
			_jobClient.Enqueue(() => _cacheManager.RemoveByTagsAsync(_categoryTags));
		}
		public void ClearCategoryDataCache()
		{
			_jobClient.Enqueue(() => _cacheManager.RemoveByTagAsync(CACHEWITHDATA));
		}

		public void NotifyAdminError(string message, string? stackTrace = null)
		{
			_jobClient.Enqueue<IErrorNotificationService>(_ => _.SendErrorNotificationAsync(message, stackTrace));
		}

		public async Task SetCategoryListCacheAsync(object data,string? search, bool? isActive, bool? isDeleted, TimeSpan? expiration = null)
		{
			var cacheKey = GetCategoryListKey(search, isActive, isDeleted);
			await _cacheManager.SetAsync(cacheKey, data, expiration ?? TimeSpan.FromMinutes(30), new string[] { CACHELIST});
		}

		public async Task<T?> GetCategoryListCacheAsync<T>(string? search, bool? isActive, bool? isDeleted)
		{
			var cacheKey = GetCategoryListKey(search,isActive, isDeleted);
			return await _cacheManager.GetAsync<T>(cacheKey);
		}

		public async Task SetCategoryByIdCacheAsync(int id, bool? isActive, bool? isDeleted, object data, TimeSpan? expiration = null)
		{
			var cacheKey = GetCategoryByIdKey(id, isActive, isDeleted);
			await _cacheManager.SetAsync(cacheKey, data, expiration ?? TimeSpan.FromMinutes(30),new string[]{ CACHEWITHDATA });
		}

		public async Task<T?> GetCategoryByIdCacheAsync<T>(int id, bool? isActive, bool? isDeleted)
		{
			var cacheKey = GetCategoryByIdKey(id, isActive, isDeleted);
			return await _cacheManager.GetAsync<T>(cacheKey);
		}
	}
}
