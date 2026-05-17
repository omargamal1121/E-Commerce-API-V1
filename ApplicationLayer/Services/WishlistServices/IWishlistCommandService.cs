using Application.DtoModels.Responses;

namespace Application.Services.WishlistServices
{
    public interface IWishlistCommandService
    {
        Task<Result<bool>> AddAsync(string userId, int productId);
        Task<Result<bool>> RemoveAsync(string userId, int productId);
        Task<Result<bool>> ClearAsync(string userId);
    }
}


