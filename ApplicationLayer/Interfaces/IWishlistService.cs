using Application.DtoModels.ProductDtos;
using Application.DtoModels.Responses;
using Application.Services;

namespace Application.Interfaces
{
    public interface IWishlistService
    {
        Task<Result<List<WishlistItemDto>>> GetWishlistAsync(string userId, int page = 1, int pageSize = 20);
        Task<Result<bool>> AddAsync(string userId, int productId);
        Task<Result<bool>> RemoveAsync(string userId, int productId);
        Task<Result<bool>> ClearAsync(string userId);
        Task<Result<bool>> IsInWishlistAsync(string userId, int productId);
    }
}


