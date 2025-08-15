using E_Commerce.DtoModels.DiscoutDtos;
using E_Commerce.DtoModels.Responses;
using E_Commerce.Enums;
using E_Commerce.Services.ProductServices;

namespace E_Commerce.Services.Discount
{
	public interface IDiscountService
	{
		// Basic CRUD Operations
		Task<Result<List<DiscountDto>>> GetAllAsync();
		Task<Result<DiscountDto>> GetDiscountByIdAsync(int id, bool? isActive = null, bool? isDeleted = null);
		Task<Result<DiscountDto>> CreateDiscountAsync(CreateDiscountDto dto, string userId);
		Task<Result<DiscountDto>> UpdateDiscountAsync(int id, UpdateDiscountDto dto, string userId);
		Task<Result<bool>> DeleteDiscountAsync(int id, string userId);
		Task<Result<DiscountDto>> RestoreDiscountAsync(int id, string userId);

		// Filtering and Search
		Task<Result<List<DiscountDto>>> FilterAsync(string? search, bool? isActive, bool? isDeleted, int page, int pageSize, string role);
		Task<Result<List<DiscountDto>>> GetActiveDiscountsAsync();
		Task<Result<List<DiscountDto>>> GetExpiredDiscountsAsync();
		Task<Result<List<DiscountDto>>> GetUpcomingDiscountsAsync();
		Task<Result<List<DiscountDto>>> GetDiscountsByCategoryAsync(int categoryId);

		// Status Management
		Task<Result<bool>> ActivateDiscountAsync(int id, string userId);
		Task<Result<bool>> DeactivateDiscountAsync(int id, string userId);


		public Task<Result<List<DiscountDto>>> GetDiscountByNameAsync(string name, bool? isActive = null, bool? isDeleted = null);
		// Validation
		Task<Result<bool>> IsDiscountValidAsync(int id);
		Task<Result<decimal>> CalculateDiscountedPriceAsync(int discountId, decimal originalPrice);


	}
} 