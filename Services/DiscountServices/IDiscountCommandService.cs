using E_Commerce.DtoModels.DiscoutDtos;
using E_Commerce.DtoModels.Responses;

namespace E_Commerce.Services.Discount
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
