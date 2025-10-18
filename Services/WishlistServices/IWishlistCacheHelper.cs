using E_Commerce.DtoModels.ProductDtos;

namespace E_Commerce.Services.WishlistServices
{
    public interface IWishlistCacheHelper
    {
         Task<HashSet<int>?> GetCachedWishlistidsAsync(string userId);
        Task CacheWishlistAsync(string userId, int page, int pageSize, bool isadmin, bool all, List<WishlistItemDto> items);
        Task InvalidateWishlistCacheAsync(string userId);
         Task CacheWishlistIdsAsync(string userid, HashSet<int> items);
        Task<List<WishlistItemDto>?> GetCachedWishlistAsync(string userId, int page, int pageSize, bool isadmin, bool all);

    }
}
