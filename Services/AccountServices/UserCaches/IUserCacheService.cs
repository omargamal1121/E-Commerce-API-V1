namespace E_Commerce.Services.AccountServices.UserCaches
{
	public interface IUserCacheService
    {
        Task SetAsync<T>(string userId, T data, TimeSpan? expiry = null);
        Task<T?> GetAsync<T>(string userId);
        Task DeleteByUserIdAsync(string userId);
    }
}
