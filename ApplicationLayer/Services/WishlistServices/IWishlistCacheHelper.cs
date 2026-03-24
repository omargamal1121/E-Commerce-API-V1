using ApplicationLayer.DtoModels.ProductDtos;

namespace ApplicationLayer.Services.WishlistServices
{
    public interface IWishlistCacheHelper
    {
        Task CacheWishlistAsync(string cacheKey, string userId, List<WishlistItemDto> items);
        Task InvalidateWishlistCacheAsync(string userId);
        string GetWishlistCacheKey(string userId, int page, int pageSize);
    }
}


