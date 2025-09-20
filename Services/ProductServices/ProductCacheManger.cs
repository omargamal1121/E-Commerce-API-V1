
using E_Commerce.Services.Cache;
using E_Commerce.Services.Collection;
using E_Commerce.Services.EmailServices;
using E_Commerce.Services.SubCategoryServices;
using Hangfire;

namespace E_Commerce.Services.ProductServices
{
	public class ProductCacheManger : IProductCacheManger
	{
		private readonly IBackgroundJobClient _jobClient;
		private readonly ICacheManager _cacheManager;
		private readonly string[] _ProductTags = new[] { "Product", "Productwithdata", "ProductList" };
		private const string _ProductListKey = "ProductList";
		private const string _Productwithdata = "Productwithdata";


		private static string GetProductListKey(string? search, bool? isActive, bool? isDeleted,int pageSize=10,int page=1,string? tag=null, bool IsAdmin=false)
			=> $"Product:list_active:{isActive}_deleted:{isDeleted}_search:{search}_pageSize:{pageSize}_page:{page}_tag:{tag}_IsAdmin:{IsAdmin}";

		private static string GetProductByIdKey(int id, bool? isActive, bool? isDeleted, bool IsAdmin = false)
			=> $"Product:{id}_active:{isActive}_deleted:{isDeleted}_IsAdmin:{IsAdmin}";

		private static string GetProductBySubCategoryIdKey(int id, bool? isActive, bool? isDeleted, int page = 1, int pageSize = 10, bool IsAdmin = false)
			=> $"Subcategory:{id}_active:{isActive}_deleted:{isDeleted}_page:{page}_pageSize:{pageSize}_IsAdmin:{IsAdmin}";

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

		public void SetProductListCacheAsync(object data, string? search, bool? isActive, bool? isDeleted, int pageSize = 10, int page = 1, string? tag = null, bool IsAdmin = false, TimeSpan? expiration = null)
		{
			var cacheKey = GetProductListKey(search, isActive, isDeleted, pageSize, page, tag, IsAdmin);
			_jobClient.Enqueue(() => _cacheManager.SetAsync(cacheKey, data, expiration ?? TimeSpan.FromMinutes(30), new string[] { _ProductListKey }));
		}

		public async Task<T?> GetProductListCacheAsync<T>(string? search, bool? isActive, bool? isDeleted, int pageSize = 10, int page = 1, string? tag = null, bool IsAdmin = false)
		{
			var cacheKey = GetProductListKey(search, isActive, isDeleted, pageSize, page, tag, IsAdmin);
			return await _cacheManager.GetAsync<T>(cacheKey);
		}
		public void SetProductListBySubCategoryidCacheAsync(object data, int subcateogryid, bool? isActive, bool? isDeleted, int page = 1, int pageSize = 10, bool IsAdmin = false, TimeSpan? expiration = null)
		{
			var cacheKey = GetProductBySubCategoryIdKey(subcateogryid, isActive, isDeleted, page, pageSize, IsAdmin);
			_jobClient.Enqueue(() => _cacheManager.SetAsync(cacheKey, data, expiration ?? TimeSpan.FromMinutes(30), new string [] { _ProductListKey}));
		}

		public async Task<T?> GetProductListBySubcategoryidCacheAsync<T>(int subcateogryid, bool? isActive, bool? isDeleted, int page = 1, int pageSize = 10, bool IsAdmin = false)
		{
			var cacheKey = GetProductBySubCategoryIdKey(subcateogryid, isActive, isDeleted, page, pageSize, IsAdmin);
			return await _cacheManager.GetAsync<T>(cacheKey);
		}

		public void SetProductByIdCacheAsync(int id, bool? isActive, bool? isDeleted, object data, bool IsAdmin = false, TimeSpan? expiration = null)
		{
			var cacheKey = GetProductByIdKey(id, isActive, isDeleted, IsAdmin);
			_jobClient.Enqueue(() => _cacheManager.SetAsync(cacheKey, data, expiration ?? TimeSpan.FromMinutes(30),new string[] { _Productwithdata}));
		}

		public async Task<T?> GetProductByIdCacheAsync<T>(int id, bool? isActive, bool? isDeleted, bool IsAdmin = false)
		{
			var cacheKey = GetProductByIdKey(id, isActive, isDeleted, IsAdmin);
			return await _cacheManager.GetAsync<T>(cacheKey);
		}
	}
}
