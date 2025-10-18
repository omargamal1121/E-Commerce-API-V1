using E_Commerce.DtoModels.ProductDtos;
using E_Commerce.DtoModels.Responses;

namespace E_Commerce.Services.WishlistServices
{
    public interface IWishlistQueryService
    {
        Task<Result<List<WishlistItemDto>>> GetWishlistAsync(string userId, bool all = false, int page = 1, int pageSize = 20,bool isadmin=false);
         Task<HashSet<int>> GetUserWishlistProductIdsAsync(string userId);
        Task<Result<bool>> IsInWishlistAsync(string userId, int productId);
    }
}
