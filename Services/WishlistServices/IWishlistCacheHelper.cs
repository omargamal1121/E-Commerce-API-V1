using E_Commerce.DtoModels.ProductDtos;

namespace E_Commerce.Services.WishlistServices
{
    public interface IWishlistCacheHelper
    {
        Task CacheWishlistAsync(string cacheKey, List<WishlistItemDto> items);
        Task InvalidateWishlistCacheAsync(string userId);
        string GetWishlistCacheKey(string userId, int page, int pageSize);
    }
}
