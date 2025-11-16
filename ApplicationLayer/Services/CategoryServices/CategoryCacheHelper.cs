using ApplicationLayer.Services.Cache;
using ApplicationLayer.Services.EmailServices;
using Hangfire;

namespace ApplicationLayer.Services.CategoryServices
{
	public class CategoryCacheHelper : ICategoryCacheHelper
	{
		private readonly IBackgroundJobClient _jobClient;
		private readonly ICacheManager _cacheManager;
		private readonly string[] _categoryTags = new[] { "category", "categorywithdata", "categorylist" };
		private const string CACHEWITHDATA = "categorywithdata";
		private const string CACHELIST = "categorylist";

		private static string GetCategoryListKey(string? search, bool? isActive, bool? isDeleted, int page, int pageSize, bool IsAdmin = false)
			=> $"category:list_active:{isActive}_deleted:{isDeleted}_search:{search}_page:{page}_pageSize:{pageSize}_IsAdmin:{IsAdmin}";

		private static string GetCategoryByIdKey(int id, bool? isActive, bool? isDeleted,bool IsAdmin=false)
			=> $"category:{id}_active:{isActive}_deleted:{isDeleted}_IsAdmin:{IsAdmin}";

		public CategoryCacheHelper(IBackgroundJobClient jobClient, ICacheManager cacheManager)
		{
			_jobClient = jobClient;
			_cacheManager = cacheManager;
		}

		public void ClearCategoryListCache()
		{
			_jobClient.Enqueue(() => _cacheManager.RemoveByTagsAsync(new string[] { CACHELIST }));
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

		public  void SetCategoryListCacheAsync(object data, string? search, bool? isActive, bool? isDeleted, int page, int pageSize, bool IsAdmin = false, TimeSpan? expiration = null)
		{
			var cacheKey = GetCategoryListKey(search, isActive, isDeleted, page, pageSize, IsAdmin);
			_jobClient.Enqueue(()=> _cacheManager.SetAsync(cacheKey, data, expiration ?? TimeSpan.FromMinutes(30), new string[] { CACHELIST }));
		}

		public async Task<T?> GetCategoryListCacheAsync<T>(string? search, bool? isActive, bool? isDeleted, int page, int pageSize, bool IsAdmin = false)
		{
			var cacheKey = GetCategoryListKey(search, isActive, isDeleted, page, pageSize, IsAdmin);
			return await _cacheManager.GetAsync<T>(cacheKey);
		}

		public void SetCategoryByIdCacheAsync(int id, bool? isActive, bool? isDeleted, object data,bool IsAdmin=false, TimeSpan? expiration = null)
		{
			var cacheKey = GetCategoryByIdKey(id, isActive, isDeleted,IsAdmin);
			_jobClient.Enqueue(()=> _cacheManager.SetAsync(cacheKey, data, expiration ?? TimeSpan.FromMinutes(30), new string[]{ CACHEWITHDATA }));
		}

		public async Task<T?> GetCategoryByIdCacheAsync<T>(int id, bool? isActive, bool? isDeleted, bool IsAdmin = false)
		{
			var cacheKey = GetCategoryByIdKey(id, isActive, isDeleted, IsAdmin);
			return await _cacheManager.GetAsync<T>(cacheKey);
		}
	}
}


