using E_Commerce.DtoModels.DiscoutDtos;
using E_Commerce.DtoModels.Responses;

namespace E_Commerce.Services.DiscountServices
{
	public class DiscountService : IDiscountService
	{
        private readonly IDiscountCommandService _discountCommandService;
        private readonly IDiscountQueryService _discountQueryService;

		public DiscountService(
            IDiscountCommandService discountCommandService,
            IDiscountQueryService discountQueryService)
        {
            _discountCommandService = discountCommandService ?? throw new ArgumentNullException(nameof(discountCommandService));
            _discountQueryService = discountQueryService ?? throw new ArgumentNullException(nameof(discountQueryService));
        }

		public async Task<Result<List<DiscountDto>>> GetAllAsync()
		{
            return await _discountQueryService.GetAllAsync();
        }

        public async Task<Result<DiscountDto>> GetDiscountByIdAsync(int id, bool? isActive = null, bool? isDeleted = null)
        {
            return await _discountQueryService.GetDiscountByIdAsync(id, isActive, isDeleted);
		}

		public async Task<Result<DiscountDto>> CreateDiscountAsync(CreateDiscountDto dto, string userId)
		{
            return await _discountCommandService.CreateDiscountAsync(dto, userId);
		}

		public async Task<Result<DiscountDto>> UpdateDiscountAsync(int id, UpdateDiscountDto dto, string userId)
		{
            return await _discountCommandService.UpdateDiscountAsync(id, dto, userId);
        }

		public async Task<Result<bool>> DeleteDiscountAsync(int id, string userId)
		{
            return await _discountCommandService.DeleteDiscountAsync(id, userId);
		}

		public async Task<Result<DiscountDto>> RestoreDiscountAsync(int id, string userId)
		{
            return await _discountCommandService.RestoreDiscountAsync(id, userId);
        }

        public async Task<Result<List<DiscountDto>>> FilterAsync(string? search, bool? isActive, bool? isDeleted, int page, int pageSize, string role)
        {
            return await _discountQueryService.FilterAsync(search, isActive, isDeleted, page, pageSize, role);
		}

		public async Task<Result<List<DiscountDto>>> GetActiveDiscountsAsync()
		{
            return await _discountQueryService.GetActiveDiscountsAsync();
		}

		public async Task<Result<List<DiscountDto>>> GetExpiredDiscountsAsync()
		{
            return await _discountQueryService.GetExpiredDiscountsAsync();
        }

		public async Task<Result<List<DiscountDto>>> GetUpcomingDiscountsAsync()
		{
            return await _discountQueryService.GetUpcomingDiscountsAsync();
        }

		public async Task<Result<List<DiscountDto>>> GetDiscountsByCategoryAsync(int categoryId)
		{
            return await _discountQueryService.GetDiscountsByCategoryAsync(categoryId);
        }

		public async Task<Result<bool>> ActivateDiscountAsync(int id, string userId)
		{
            return await _discountCommandService.ActivateDiscountAsync(id, userId);
		}

		public async Task<Result<bool>> DeactivateDiscountAsync(int id, string userId)
		{
            return await _discountCommandService.DeactivateDiscountAsync(id, userId);
        }

        public async Task<Result<List<DiscountDto>>> GetDiscountByNameAsync(string name, bool? isActive = null, bool? isDeleted = null)
        {
            return await _discountQueryService.GetDiscountByNameAsync(name, isActive, isDeleted);
		}

		public async Task<Result<bool>> IsDiscountValidAsync(int id)
		{
            return await _discountQueryService.IsDiscountValidAsync(id);
		}

		public async Task<Result<decimal>> CalculateDiscountedPriceAsync(int discountId, decimal originalPrice)
		{
            return await _discountQueryService.CalculateDiscountedPriceAsync(discountId, originalPrice);
        }
	}
} 