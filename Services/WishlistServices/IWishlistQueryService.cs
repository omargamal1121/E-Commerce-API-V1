using E_Commerce.DtoModels.ProductDtos;
using E_Commerce.DtoModels.Responses;

namespace E_Commerce.Services.WishlistServices
{
    public interface IWishlistQueryService
    {
        Task<Result<List<WishlistItemDto>>> GetWishlistAsync(string userId, int page = 1, int pageSize = 20);
        Task<Result<bool>> IsInWishlistAsync(string userId, int productId);
    }
}
