using E_Commerce.DtoModels.DiscoutDtos;
using E_Commerce.DtoModels.Responses;

namespace E_Commerce.Services.Discount
{
    public interface IDiscountQueryService
    {
        Task<Result<List<DiscountDto>>> GetAllAsync();
        Task<Result<DiscountDto>> GetDiscountByIdAsync(int id, bool? isActive = null, bool? isDeleted = null);
        Task<Result<List<DiscountDto>>> GetDiscountByNameAsync(string name, bool? isActive = null, bool? isDeleted = null);
        Task<Result<List<DiscountDto>>> FilterAsync(string? search, bool? isActive, bool? isDeleted, int page, int pageSize, string role);
        Task<Result<List<DiscountDto>>> GetActiveDiscountsAsync();
        Task<Result<List<DiscountDto>>> GetExpiredDiscountsAsync();
        Task<Result<List<DiscountDto>>> GetUpcomingDiscountsAsync();
        Task<Result<List<DiscountDto>>> GetDiscountsByCategoryAsync(int categoryId);
        Task<Result<bool>> IsDiscountValidAsync(int id);
        Task<Result<decimal>> CalculateDiscountedPriceAsync(int discountId, decimal originalPrice);
    }
}
