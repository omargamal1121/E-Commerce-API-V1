
using E_Commerce.Services.Cache;
using E_Commerce.Services.EmailServices;
using Hangfire;

namespace E_Commerce.Services.ProductServices
{
	public class ProductCacheManger : IProductCacheManger
	{
		private readonly IBackgroundJobClient _jobClient;
		private readonly ICacheManager _cacheManager;
		private readonly string[] _ProductTags = new[] { "Product", "Productwithdata" };
		private const string _ProductListKey = "Product";
		private const string _Productwithdata = "Productwithdata";


		private static string GetProductListKey(string? search, bool? isActive, bool? isDeleted)
			=> $"Product:list_active:{isActive}_deleted:{isDeleted}_search:{search}";

		private static string GetProductByIdKey(int id, bool? isActive, bool? isDeleted)
			=> $"Product:{id}_active:{isActive}_deleted:{isDeleted}";

		private static string GetProductBySubCategoryIdKey(int id, bool? isActive, bool? isDeleted)
			=> $"Subcategory:{id}_active:{isActive}_deleted:{isDeleted}";

		public ProductCacheManger(IBackgroundJobClient jobClient, ICacheManager cacheManager)
		{
			_jobClient = jobClient;
			_cacheManager = cacheManager;
		}

		public void ClearProductCache()
		{
			_jobClient.Enqueue(() => _cacheManager.RemoveByTagsAsync(_ProductTags));
		}
	
		public void ClearProductListCache()
		{
			_jobClient.Enqueue(() => _cacheManager.RemoveByTagAsync(_ProductListKey));
		}
		public void ClearProductDataCache()
		{
			_jobClient.Enqueue(() => _cacheManager.RemoveByTagAsync(_Productwithdata));
		}

		public void NotifyAdminError(string message, string? stackTrace = null)
		{
			_jobClient.Enqueue<IErrorNotificationService>(_ => _.SendErrorNotificationAsync(message, stackTrace));
		}

		public async Task SetProductListCacheAsync(object data, string? search, bool? isActive, bool? isDeleted, TimeSpan? expiration = null)
		{
			var cacheKey = GetProductListKey(search, isActive, isDeleted);
			await _cacheManager.SetAsync(cacheKey, data, expiration ?? TimeSpan.FromMinutes(30), _ProductTags);
		}

		public async Task<T?> GetProductListCacheAsync<T>(string? search, bool? isActive, bool? isDeleted)
		{
			var cacheKey = GetProductListKey(search, isActive, isDeleted);
			return await _cacheManager.GetAsync<T>(cacheKey);
		}
		public async Task SetProductListBySubCategoryidCacheAsync(object data, int subcateogryid, bool? isActive, bool? isDeleted, TimeSpan? expiration = null)
		{
			var cacheKey = GetProductBySubCategoryIdKey(subcateogryid, isActive, isDeleted);
			await _cacheManager.SetAsync(cacheKey, data, expiration ?? TimeSpan.FromMinutes(30), new string [] { _ProductListKey});
		}

		public async Task<T?> GetProductListBySubcategoryidCacheAsync<T>(int subcateogryid, bool? isActive, bool? isDeleted)
		{
			var cacheKey = GetProductBySubCategoryIdKey(subcateogryid, isActive, isDeleted);
			return await _cacheManager.GetAsync<T>(cacheKey);
		}

		public async Task SetProductByIdCacheAsync(int id, bool? isActive, bool? isDeleted, object data, TimeSpan? expiration = null)
		{
			var cacheKey = GetProductByIdKey(id, isActive, isDeleted);
			await _cacheManager.SetAsync(cacheKey, data, expiration ?? TimeSpan.FromMinutes(30),new string[] { _Productwithdata});
		}

		public async Task<T?> GetProductByIdCacheAsync<T>(int id, bool? isActive, bool? isDeleted)
		{
			var cacheKey = GetProductByIdKey(id, isActive, isDeleted);
			return await _cacheManager.GetAsync<T>(cacheKey);
		}
	}
}
