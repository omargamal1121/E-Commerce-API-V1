namespace E_Commerce.Services.CategoryServices
{
	public interface ICategoryCacheHelper
	{
		void ClearCategoryCache();
		public void ClearCategoryListCache();
		public void ClearCategoryDataCache();
		void NotifyAdminError(string message, string? stackTrace = null);
		Task SetCategoryListCacheAsync(object data,string? search, bool? isActive, bool? isDeleted, TimeSpan? expiration = null);
		Task<T?> GetCategoryListCacheAsync<T>(string? search, bool? isActive, bool? isDeleted);
		Task SetCategoryByIdCacheAsync(int id, bool? isActive, bool? isDeleted, object data, TimeSpan? expiration = null);
		Task<T?> GetCategoryByIdCacheAsync<T>(int id, bool? isActive, bool? isDeleted);
	}
}
