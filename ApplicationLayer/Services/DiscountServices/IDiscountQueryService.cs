using ApplicationLayer.DtoModels.DiscoutDtos;
using ApplicationLayer.DtoModels.Responses;

namespace ApplicationLayer.Services.DiscountServices
{
    public interface IDiscountQueryService
    {
      
        Task<Result<DiscountDto>> GetDiscountByIdAsync(int id, bool? isActive = null, bool? isDeleted = null,bool IsAdmin=false);
       
        Task<Result<List<DiscountDto>>> FilterAsync(string? search, bool? isActive, bool? isDeleted, int page, int pageSize, bool IsAdmin = false);
        Task<Result<List<DiscountDto>>> GetActiveDiscountsAsync(int page=1,int pagesize=10);
        Task<Result<List<DiscountDto>>> GetExpiredDiscountsAsync(int page = 1, int pagesize = 10);
        Task<Result<List<DiscountDto>>> GetUpcomingDiscountsAsync(int page = 1, int pagesize = 10);
        Task<Result<List<DiscountDto>>> GetDiscountsByCategoryAsync(int categoryId);
        Task<Result<bool>> IsDiscountValidAsync(int id);
        Task<Result<decimal>> CalculateDiscountedPriceAsync(int discountId, decimal originalPrice);
    }
}


