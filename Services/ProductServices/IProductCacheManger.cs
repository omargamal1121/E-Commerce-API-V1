namespace E_Commerce.Services.ProductServices
{
	public interface IProductCacheManger
	{
		void ClearProductCache();
		public  Task<T?> GetProductListBySubcategoryidCacheAsync<T>(int subcateogryid, bool? isActive, bool? isDeleted, int page = 1, int pageSize = 10, bool IsAdmin = false);
		public void SetProductListBySubCategoryidCacheAsync(object data, int subcateogryid, bool? isActive, bool? isDeleted, int page = 1, int pageSize = 10, bool IsAdmin = false, TimeSpan? expiration = null);

		void NotifyAdminError(string message, string? stackTrace = null);
		void SetProductListCacheAsync(object data, string? search, bool? isActive, bool? isDeleted,int pageSize=10,int page=1,string? tag=null,bool IsAdmin = false, TimeSpan? expiration = null);
		Task<T?> GetProductListCacheAsync<T>(string? search, bool? isActive, bool? isDeleted, int pageSize = 10, int page = 1, string? tag = null, bool IsAdmin = false);
		void SetProductByIdCacheAsync(int id, bool? isActive, bool? isDeleted, object data, bool IsAdmin = false, TimeSpan? expiration = null);
		Task<T?> GetProductByIdCacheAsync<T>(int id, bool? isActive, bool? isDeleted, bool IsAdmin = false);
	}
}
