namespace ApplicationLayer.Services.CategoryServices
{
	public interface ICategoryCacheHelper
	{
		void ClearCategoryCache();
		public void ClearCategoryListCache();
		public void ClearCategoryDataCache();
		void NotifyAdminError(string message, string? stackTrace = null);
		void SetCategoryListCacheAsync(object data, string? search, bool? isActive, bool? isDeleted, int page, int pageSize, bool IsAdmin = false, TimeSpan? expiration = null);
		Task<T?> GetCategoryListCacheAsync<T>(string? search, bool? isActive, bool? isDeleted, int page, int pageSize, bool IsAdmin = false);
		void SetCategoryByIdCacheAsync(int id, bool? isActive, bool? isDeleted, object data, bool IsAdmin = false, TimeSpan? expiration = null);
		Task<T?> GetCategoryByIdCacheAsync<T>(int id, bool? isActive, bool? isDeleted, bool IsAdmin = false);
	}
}


