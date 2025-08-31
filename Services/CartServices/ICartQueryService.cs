using E_Commerce.DtoModels.CartDtos;
using E_Commerce.DtoModels.Responses;

namespace E_Commerce.Services.CartServices
{
    public interface ICartQueryService
    {
        Task<Result<CartDto>> GetCartAsync(string userId);
        Task<Result<int?>> GetCartItemCountAsync(string userId);
        Task<Result<bool>> IsCartEmptyAsync(string userId);
    }
}
