namespace ApplicationLayer.Services.CartServices
{
    public interface ICartCacheHelper
    {
        void ClearCartCache();
        void NotifyAdminError(string message, string? stackTrace = null);
        Task SetCartCacheAsync(string userId, object data, TimeSpan? expiration = null);
        Task<T?> GetCartCacheAsync<T>(string userId);
        Task RemoveCartCacheAsync(string userId);
    }
}


