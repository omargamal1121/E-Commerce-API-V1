using ApplicationLayer.DtoModels.DiscoutDtos;
using ApplicationLayer.DtoModels.Responses;

namespace ApplicationLayer.Services.DiscountServices
{
    public interface IDiscountCommandService
    {
        Task<Result<DiscountDto>> CreateDiscountAsync(CreateDiscountDto dto, string userId);
        Task<Result<DiscountDto>> UpdateDiscountAsync(int id, UpdateDiscountDto dto, string userId);
        Task<Result<bool>> DeleteDiscountAsync(int id, string userId);
        Task<Result<DiscountDto>> RestoreDiscountAsync(int id, string userId);
        Task<Result<bool>> ActivateDiscountAsync(int id, string userId);
        Task<Result<bool>> DeactivateDiscountAsync(int id, string userId);
        Task<Result<bool>> UpdateCartPricesOnDiscountChange(int discountId);
    }
}


