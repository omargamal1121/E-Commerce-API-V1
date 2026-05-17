using Application.DtoModels.ProductDtos;
using Application.DtoModels.Responses;

namespace Application.Services.WishlistServices
{
    public interface IWishlistQueryService
    {
        Task<Result<List<WishlistItemDto>>> GetWishlistAsync(string userId, int page = 1, int pageSize = 20);
        Task<Result<bool>> IsInWishlistAsync(string userId, int productId);
    }
}


