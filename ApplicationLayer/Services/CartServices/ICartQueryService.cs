using Application.DtoModels.CartDtos;
using Application.DtoModels.Responses;

namespace Application.Services.CartServices
{
    public interface ICartQueryService
    {
        Task<Result<CartDto>> GetCartAsync(string userId);
        Task<Result<int?>> GetCartItemCountAsync(string userId);
        Task<Result<bool>> IsCartEmptyAsync(string userId);
    }
}


