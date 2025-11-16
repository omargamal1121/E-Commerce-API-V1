using ApplicationLayer.DtoModels.DiscoutDtos;
using ApplicationLayer.DtoModels.Responses;
using DomainLayer.Enums;
using ApplicationLayer.Services.ProductServices;

namespace ApplicationLayer.Services.DiscountServices
{
	public interface IDiscountService
	{
		// Basic CRUD Operations

		Task<Result<DiscountDto>> GetDiscountByIdAsync(int id, bool? isActive = null, bool? isDeleted = null);
		Task<Result<DiscountDto>> CreateDiscountAsync(CreateDiscountDto dto, string userId);
		Task<Result<DiscountDto>> UpdateDiscountAsync(int id, UpdateDiscountDto dto, string userId);
		Task<Result<bool>> DeleteDiscountAsync(int id, string userId);
		Task<Result<DiscountDto>> RestoreDiscountAsync(int id, string userId);

		// Filtering and Search
		Task<Result<List<DiscountDto>>> FilterAsync(string? search, bool? isActive, bool? isDeleted, int page, int pageSize, bool isAdmin);
		Task<Result<List<DiscountDto>>> GetActiveDiscountsAsync(int page = 1, int pagesize = 10);
		Task<Result<List<DiscountDto>>> GetExpiredDiscountsAsync(int page = 1, int pagesize = 10);
		Task<Result<List<DiscountDto>>> GetUpcomingDiscountsAsync(int page = 1, int pagesize = 10);
		Task<Result<List<DiscountDto>>> GetDiscountsByCategoryAsync(int categoryId);

		// Status Management
		Task<Result<bool>> ActivateDiscountAsync(int id, string userId);
		Task<Result<bool>> DeactivateDiscountAsync(int id, string userId);



		// Validation
		Task<Result<bool>> IsDiscountValidAsync(int id);
		Task<Result<decimal>> CalculateDiscountedPriceAsync(int discountId, decimal originalPrice);


	}
} 

