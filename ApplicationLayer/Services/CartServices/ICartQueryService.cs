using ApplicationLayer.DtoModels.CartDtos;
using ApplicationLayer.DtoModels.Responses;

namespace ApplicationLayer.Services.CartServices
{
    public interface ICartQueryService
    {
        Task<Result<CartDto>> GetCartAsync(string userId);
        Task<Result<int?>> GetCartItemCountAsync(string userId);
        Task<Result<bool>> IsCartEmptyAsync(string userId);
    }
}


