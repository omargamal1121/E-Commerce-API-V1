using E_Commerce.DtoModels.DiscoutDtos;
using E_Commerce.DtoModels.Responses;
using E_Commerce.UOW;
using Hangfire;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace E_Commerce.Services.DiscountServices
{
    public class DiscountQueryService : IDiscountQueryService
    {
        private readonly ILogger<DiscountQueryService> _logger;
        private readonly IBackgroundJobClient _backgroundJobClient;
        private readonly IUnitOfWork _unitOfWork;
        private readonly IDiscountCacheHelper _cacheHelper;
        private readonly IDiscountMapper _mapper;

        public DiscountQueryService(
            ILogger<DiscountQueryService> logger,
            IBackgroundJobClient backgroundJobClient,
            IUnitOfWork unitOfWork,
            IDiscountCacheHelper cacheHelper,
            IDiscountMapper mapper)
        {
            _logger = logger;
            _backgroundJobClient = backgroundJobClient;
            _unitOfWork = unitOfWork;
            _cacheHelper = cacheHelper;
            _mapper = mapper;
        }

        public async Task<Result<List<DiscountDto>>> GetAllAsync()
        {
            try
            {
                var discounts = await _unitOfWork.Repository<Models.Discount>().GetAll().AsNoTracking()
                    .Where(d => d.DeletedAt == null)
                    .Select(_mapper.DiscountDtoSelector)
                    .ToListAsync();

                if (!discounts.Any())
                    return Result<List<DiscountDto>>.Fail("No discounts found", 404);

                return Result<List<DiscountDto>>.Ok(discounts, "All discounts retrieved successfully", 200);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in GetAllAsync");
                _cacheHelper.NotifyAdminError($"Error in GetAllAsync: {ex.Message}", ex.StackTrace);
                return Result<List<DiscountDto>>.Fail("Error retrieving discounts", 500);
            }
        }

        public async Task<Result<DiscountDto>> GetDiscountByIdAsync(int id, bool? isActive = null, bool? isDeleted = false)
        {
            try
            {
                var query = _unitOfWork.Repository<Models.Discount>().GetAll().AsNoTracking().Where(d => d.Id == id);

                if (isDeleted.HasValue)
                {
                    if (isDeleted.Value)
                        query = query.Where(d => d.DeletedAt != null);
                    else
                        query = query.Where(d => d.DeletedAt == null);
                }
                if (isActive.HasValue)
                    query = query.Where(d => d.IsActive == isActive.Value);

                var discount = await query
                    .Select(_mapper.DiscountDtoSelector)
                    .FirstOrDefaultAsync();

                if (discount == null)
                    return Result<DiscountDto>.Fail("Discount not found", 404);

                return Result<DiscountDto>.Ok(discount, "Discount retrieved successfully", 200);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error in GetDiscountByIdAsync for id: {id}");
                _cacheHelper.NotifyAdminError($"Error in GetDiscountByIdAsync for id {id}: {ex.Message}", ex.StackTrace);
                return Result<DiscountDto>.Fail("Error retrieving discount", 500);
            }
        }

        public async Task<Result<List<DiscountDto>>> GetDiscountByNameAsync(string name, bool? isActive = null, bool? isDeleted = null)
        {
            try
            {
                var query = _unitOfWork.Repository<Models.Discount>().GetAll().AsNoTracking()
                    .Where(d => d.Name.Contains(name) || d.Description.Contains(name));
                if (isDeleted.HasValue)
                {
                    if (isDeleted.Value)
                        query = query.Where(d => d.DeletedAt != null);
                    else
                        query = query.Where(d => d.DeletedAt == null);
                }
                if (isActive.HasValue)
                    query = query.Where(d => d.IsActive == isActive.Value);
                var discount = await query
                    .Select(_mapper.DiscountDtoSelector)
                    .ToListAsync();

                if (discount == null)
                    return Result<List<DiscountDto>>.Fail("Discount not found", 404);
                return Result<List<DiscountDto>>.Ok(discount, "Discount retrieved successfully", 200);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error in GetDiscountByNameAsync for name: {name}");
                _cacheHelper.NotifyAdminError($"Error in GetDiscountByNameAsync for name {name}: {ex.Message}", ex.StackTrace);
                return Result<List<DiscountDto>>.Fail("Error retrieving discount", 500);
            }
        }

        public async Task<Result<List<DiscountDto>>> FilterAsync(string? search, bool? isActive, bool? isDeleted, int page, int pageSize, string role)
        {
            try
            {
                var query = _unitOfWork.Repository<Models.Discount>().GetAll().AsNoTracking();

                if (isDeleted.HasValue)
                {
                    if (isDeleted.Value)
                        query = query.Where(d => d.DeletedAt != null);
                    else
                        query = query.Where(d => d.DeletedAt == null);
                }

                if (!string.IsNullOrWhiteSpace(search))
                    query = query.Where(d => d.Name.Contains(search) || d.Description.Contains(search));

                if (isActive.HasValue)
                    query = query.Where(d => d.IsActive == isActive.Value);

                var totalCount = await query.CountAsync();

                var discounts = await query
                    .OrderBy(d => d.CreatedAt)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .Select(_mapper.DiscountDtoSelector)
                    .ToListAsync();

                if (!discounts.Any())
                    return Result<List<DiscountDto>>.Fail("No discounts found matching criteria", 404);

                return Result<List<DiscountDto>>.Ok(discounts, $"Found {discounts.Count} discounts out of {totalCount}", 200);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in FilterAsync");
                _cacheHelper.NotifyAdminError($"Error in FilterAsync: {ex.Message}", ex.StackTrace);
                return Result<List<DiscountDto>>.Fail("Error filtering discounts", 500);
            }
        }

        public async Task<Result<List<DiscountDto>>> GetActiveDiscountsAsync()
        {
            try
            {
                var now = DateTime.UtcNow;
                var discounts = await _unitOfWork.Repository<Models.Discount>().GetAll().AsNoTracking()
                    .Where(d => d.DeletedAt == null &&
                        d.IsActive &&
                        d.StartDate <= now &&
                        d.EndDate >= now)
                    .Select(_mapper.DiscountDtoSelector)
                    .ToListAsync();

                if (!discounts.Any())
                    return Result<List<DiscountDto>>.Fail("No active discounts found", 404);

                return Result<List<DiscountDto>>.Ok(discounts, "Active discounts retrieved successfully", 200);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in GetActiveDiscountsAsync");
                _cacheHelper.NotifyAdminError($"Error in GetActiveDiscountsAsync: {ex.Message}", ex.StackTrace);
                return Result<List<DiscountDto>>.Fail("Error retrieving active discounts", 500);
            }
        }

        public async Task<Result<List<DiscountDto>>> GetExpiredDiscountsAsync()
        {
            try
            {
                var now = DateTime.UtcNow;

                var expiredDiscounts = await _unitOfWork.Repository<Models.Discount>().GetAll().AsNoTracking()
                    .Where(d => d.DeletedAt == null && d.EndDate < now)
                    .OrderByDescending(d => d.EndDate)
                    .Select(_mapper.DiscountDtoSelector)
                    .ToListAsync();

                if (expiredDiscounts.Count == 0)
                    return Result<List<DiscountDto>>.Ok(new List<DiscountDto>(), "No expired discounts found", 200);

                return Result<List<DiscountDto>>.Ok(expiredDiscounts, "Expired discounts retrieved successfully", 200);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in GetExpiredDiscountsAsync");
                _cacheHelper.NotifyAdminError($"Error in GetExpiredDiscountsAsync: {ex.Message}", ex.StackTrace);
                return Result<List<DiscountDto>>.Fail("Error retrieving expired discounts", 500);
            }
        }

        public async Task<Result<List<DiscountDto>>> GetUpcomingDiscountsAsync()
        {
            try
            {
                var now = DateTime.UtcNow;

                var upcomingDiscounts = await _unitOfWork.Repository<Models.Discount>().GetAll().AsNoTracking()
                    .Where(d => d.DeletedAt == null && d.StartDate > now)
                    .OrderBy(d => d.StartDate)
                    .Select(_mapper.DiscountDtoSelector)
                    .ToListAsync();

                if (upcomingDiscounts.Count == 0)
                    return Result<List<DiscountDto>>.Ok(new List<DiscountDto>(), "No upcoming discounts found", 200);

                return Result<List<DiscountDto>>.Ok(upcomingDiscounts, "Upcoming discounts retrieved successfully", 200);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in GetUpcomingDiscountsAsync");
                _cacheHelper.NotifyAdminError($"Error in GetUpcomingDiscountsAsync: {ex.Message}", ex.StackTrace);
                return Result<List<DiscountDto>>.Fail("Error retrieving upcoming discounts", 500);
            }
        }

        public async Task<Result<List<DiscountDto>>> GetDiscountsByCategoryAsync(int categoryId)
        {
            try
            {
                var discounts = await _unitOfWork.Repository<Models.Discount>().GetAll().AsNoTracking()
                    .Where(d => d.DeletedAt == null && d.CategoryId == categoryId)
                    .Select(_mapper.DiscountDtoSelector)
                    .ToListAsync();

                if (!discounts.Any())
                    return Result<List<DiscountDto>>.Fail($"No discounts found for category {categoryId}", 404);

                return Result<List<DiscountDto>>.Ok(discounts, "Category discounts retrieved successfully", 200);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error in GetDiscountsByCategoryAsync for categoryId: {categoryId}");
                _cacheHelper.NotifyAdminError($"Error in GetDiscountsByCategoryAsync for categoryId {categoryId}: {ex.Message}", ex.StackTrace);
                return Result<List<DiscountDto>>.Fail("Error retrieving category discounts", 500);
            }
        }

        public async Task<Result<bool>> IsDiscountValidAsync(int id)
        {
            try
            {
                var now = DateTime.UtcNow;

                var discount = await _unitOfWork.Repository<Models.Discount>()
                    .GetAll().AsNoTracking()
                    .Where(d => d.Id == id && d.DeletedAt == null)
                    .FirstOrDefaultAsync();

                if (discount == null)
                    return Result<bool>.Ok(false, "Discount not found", 200);

                if (!discount.IsActive)
                    return Result<bool>.Ok(false, "Discount is not active", 200);

                if (discount.StartDate > now)
                    return Result<bool>.Ok(false, "Discount has not started yet", 200);

                if (discount.EndDate < now)
                    return Result<bool>.Ok(false, "Discount has expired", 200);

                return Result<bool>.Ok(true, "Discount is valid", 200);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error in IsDiscountValidAsync for id: {id}");
                _cacheHelper.NotifyAdminError($"Error in IsDiscountValidAsync for id {id}: {ex.Message}", ex.StackTrace);
                return Result<bool>.Fail("Error validating discount", 500);
            }
        }

        public async Task<Result<decimal>> CalculateDiscountedPriceAsync(int discountId, decimal originalPrice)
        {
            try
            {
                var discount = await _unitOfWork.Repository<Models.Discount>().GetByIdAsync(discountId);
                if (discount == null)
                    return Result<decimal>.Fail("Discount not found", 404);

                var now = DateTime.UtcNow;
                if (!discount.IsActive || discount.StartDate > now || discount.EndDate < now)
                    return Result<decimal>.Ok(originalPrice, "Discount not valid, returning original price", 200);

                var discountAmount = originalPrice * (discount.DiscountPercent / 100m);
                var discountedPrice = originalPrice - discountAmount;

                return Result<decimal>.Ok(discountedPrice, "Discounted price calculated successfully", 200);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error in CalculateDiscountedPriceAsync for discountId: {discountId}");
                _cacheHelper.NotifyAdminError($"Error in CalculateDiscountedPriceAsync for discountId {discountId}: {ex.Message}", ex.StackTrace);
                return Result<decimal>.Fail("Error calculating discounted price", 500);
            }
        }
    }
}
