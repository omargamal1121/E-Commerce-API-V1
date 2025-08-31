namespace E_Commerce.Services.ProductServices
{
	public interface IProductCacheManger
	{
		void ClearProductCache();
		public  Task<T?> GetProductListBySubcategoryidCacheAsync<T>(int subcateogryid, bool? isActive, bool? isDeleted);
		public Task SetProductListBySubCategoryidCacheAsync(object data, int subcateogryid, bool? isActive, bool? isDeleted, TimeSpan? expiration = null);

		void NotifyAdminError(string message, string? stackTrace = null);
		Task SetProductListCacheAsync(object data, string? search, bool? isActive, bool? isDeleted, TimeSpan? expiration = null);
		Task<T?> GetProductListCacheAsync<T>(string? search, bool? isActive, bool? isDeleted);
		Task SetProductByIdCacheAsync(int id, bool? isActive, bool? isDeleted, object data, TimeSpan? expiration = null);
		Task<T?> GetProductByIdCacheAsync<T>(int id, bool? isActive, bool? isDeleted);
	}
}
