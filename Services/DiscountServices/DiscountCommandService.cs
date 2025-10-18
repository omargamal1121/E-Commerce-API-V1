using E_Commerce.DtoModels.DiscoutDtos;
using E_Commerce.DtoModels.Responses;
using E_Commerce.Enums;
using E_Commerce.Interfaces;
using E_Commerce.Models;
using E_Commerce.Services.AdminOperationServices;
using E_Commerce.UOW;
using Hangfire;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace E_Commerce.Services.DiscountServices
{
    public class DiscountCommandService : IDiscountCommandService
    {
        private readonly ILogger<DiscountCommandService> _logger;
        private readonly IBackgroundJobClient _backgroundJobClient;
        private readonly IUnitOfWork _unitOfWork;
        private readonly IAdminOpreationServices _adminOperationServices;
        private readonly IDiscountCacheHelper _cacheHelper;
        private readonly ICartServices _cartServices;
        private readonly IDiscountBackgroundJopMethod _discountBackgroundJopMethod;

        public DiscountCommandService(
            IDiscountBackgroundJopMethod discountBackgroundJopMethod,
            ILogger<DiscountCommandService> logger,
            IBackgroundJobClient backgroundJobClient,
            IUnitOfWork unitOfWork,
            IAdminOpreationServices adminOperationServices,
            IDiscountCacheHelper cacheHelper,
            ICartServices cartServices)
        {
            _discountBackgroundJopMethod = discountBackgroundJopMethod;
            _logger = logger;
            _backgroundJobClient = backgroundJobClient;
            _unitOfWork = unitOfWork;
            _adminOperationServices = adminOperationServices;
            _cacheHelper = cacheHelper;
            _cartServices = cartServices;
        }

        public async Task<Result<DiscountDto>> CreateDiscountAsync(CreateDiscountDto dto, string userId)
        {
            _logger.LogInformation($"Creating new discount: {dto.Name}");
                using var transaction = await _unitOfWork.BeginTransactionAsync();
            try
            {
                if (dto.StartDate >= dto.EndDate)
                    return Result<DiscountDto>.Fail("Start date must be before end date", 400);

                if (dto.EndDate < DateTime.UtcNow)
                    return Result<DiscountDto>.Fail("End date cannot be in the past", 400);

                var existingDiscount = await _unitOfWork.Repository<Models.Discount>().GetAll().AsNoTracking()
                    .FirstOrDefaultAsync(d => d.Name == dto.Name && d.DeletedAt == null);

                if (existingDiscount != null)
                    return Result<DiscountDto>.Fail($"Discount with name '{dto.Name}' already exists", 409);


                var discount = new Models.Discount
                {
                    Name = dto.Name,
                    Description = dto.Description,
                    DiscountPercent = dto.DiscountPercent,
                    StartDate = dto.StartDate,
                    EndDate = dto.EndDate,
                };

                var result = await _unitOfWork.Repository<Models.Discount>().CreateAsync(discount);
                if (result == null)
                {
                    await transaction.RollbackAsync();
                    return Result<DiscountDto>.Fail("Failed to create discount", 400);
                }

                await _adminOperationServices.AddAdminOpreationAsync(
                    $"Create Discount {discount.Id}",
                    Opreations.AddOpreation,
                    userId,
                    discount.Id
                );

                await _unitOfWork.CommitAsync();
                await transaction.CommitAsync();

                _discountBackgroundJopMethod.ScheduleDiscountCheck(discount.Id, discount.StartDate, discount.EndDate);

                var discountDto = new DiscountDto
                {
                    Id = discount.Id,
                    Name = discount.Name,
                    Description = discount.Description,
                    DiscountPercent = discount.DiscountPercent,
                    StartDate = discount.StartDate,
                    EndDate = discount.EndDate,
                    IsActive = discount.IsActive,
                    CreatedAt = discount.CreatedAt
                };
                _cacheHelper.ClearProductCache();
                return Result<DiscountDto>.Ok(discountDto, "Discount created successfully", 201);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error in CreateDiscountAsync for discount {dto.Name}");
               await  transaction.RollbackAsync();
                _discountBackgroundJopMethod.NotifyAdminError($"Error in CreateDiscountAsync for discount {dto.Name}: {ex.Message}", ex.StackTrace);
                return Result<DiscountDto>.Fail("Error creating discount", 500);
            }
        }

        public async Task<Result<DiscountDto>> UpdateDiscountAsync(int id, UpdateDiscountDto dto, string userId)
        {
            _logger.LogInformation($"Updating discount: {id}");
            try
            {
                var discount = await _unitOfWork.Repository<Models.Discount>().GetByIdAsync(id);
                if (discount == null)
                    return Result<DiscountDto>.Fail("Discount not found", 404);

                using var transaction = await _unitOfWork.BeginTransactionAsync();

                var changes = new List<string>();

                if (!string.IsNullOrEmpty(dto.Name) && dto.Name != discount.Name)
                {
                    changes.Add($"Name changed from '{discount.Name}' to '{dto.Name}'");
                    discount.Name = dto.Name;
                }
                if (!string.IsNullOrEmpty(dto.Description) && dto.Description != discount.Description)
                {
                    changes.Add($"Description changed from '{discount.Description}' to '{dto.Description}'");
                    discount.Description = dto.Description;
                }
                if (dto.DiscountPercent.HasValue && dto.DiscountPercent.Value != discount.DiscountPercent)
                {
                    changes.Add($"DiscountPercent changed from {discount.DiscountPercent}% to {dto.DiscountPercent.Value}%");
                    discount.DiscountPercent = dto.DiscountPercent.Value;
                }
                if (dto.StartDate.HasValue && dto.StartDate.Value != discount.StartDate)
                {
                    changes.Add($"StartDate changed from {discount.StartDate} to {dto.StartDate.Value}");
                    discount.StartDate = dto.StartDate.Value;
                }
                if (dto.EndDate.HasValue && dto.EndDate.Value != discount.EndDate && discount.EndDate < DateTime.UtcNow)
                {
                    changes.Add($"EndDate changed from {discount.EndDate} to {dto.EndDate.Value}");
                    discount.EndDate = dto.EndDate.Value;
                }

                if (!changes.Any())
                {
                    return Result<DiscountDto>.Fail("No changes were provided to update.", 400);
                }

                if (discount.StartDate >= discount.EndDate)
                    return Result<DiscountDto>.Fail("Start date must be before end date", 400);

                var result = _unitOfWork.Repository<Models.Discount>().Update(discount);
                if (!result)
                {
                    await transaction.RollbackAsync();
                    return Result<DiscountDto>.Fail("Failed to update discount", 400);
                }

                string changeSummary = string.Join(" | ", changes);
                await _adminOperationServices.AddAdminOpreationAsync(
                    $"Updated Discount {id}: {changeSummary}",
                    Opreations.UpdateOpreation,
                    userId,
                    id
                );

                await _unitOfWork.CommitAsync();
                await transaction.CommitAsync();
                _cacheHelper.ClearProductCache();

                _discountBackgroundJopMethod.ScheduleDiscountCheck(discount.Id, discount.StartDate, discount.EndDate);

                var discountDto = new DiscountDto
                {
                    Id = discount.Id,
                    Name = discount.Name,
                    Description = discount.Description,
                    DiscountPercent = discount.DiscountPercent,
                    StartDate = discount.StartDate,
                    EndDate = discount.EndDate,
                    IsActive = discount.IsActive,
                    CreatedAt = discount.CreatedAt,
                    DeletedAt = discount.DeletedAt,
                    ModifiedAt = discount.ModifiedAt
                };

                return Result<DiscountDto>.Ok(discountDto, "Discount updated successfully", 200);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error in UpdateDiscountAsync for id: {id}");
                _discountBackgroundJopMethod.NotifyAdminError($"Error in UpdateDiscountAsync for id {id}: {ex.Message}", ex.StackTrace);
                return Result<DiscountDto>.Fail("Error updating discount", 500);
            }
        }

        public async Task<Result<bool>> DeleteDiscountAsync(int id, string userId)
        {
            _logger.LogInformation($"Deleting discount: {id}");
            try
            {
                var discount = await _unitOfWork.Repository<Models.Discount>().GetByIdAsync(id);
                if (discount == null || discount.DeletedAt != null)
                    return Result<bool>.Fail("Discount not found", 404);

                using var transaction = await _unitOfWork.BeginTransactionAsync();

                var result = await _unitOfWork.Repository<Models.Discount>().SoftDeleteAsync(id);
                discount.IsActive = false;
                if (!result)
                {
                    await transaction.RollbackAsync();
                    return Result<bool>.Fail("Failed to delete discount", 400);
                }

                await _adminOperationServices.AddAdminOpreationAsync(
                    $"Delete Discount {id}",
                    Opreations.DeleteOpreation,
                    userId,
                    id
                );

                await _unitOfWork.CommitAsync();
                await transaction.CommitAsync();
                var productsids = await _unitOfWork.Product.GetAll().Where(p => p.DiscountId == id && p.DeletedAt == null).Select(p => p.Id).ToListAsync();

                _backgroundJobClient.Enqueue(() => _cartServices.UpdateCartItemsForProductsAfterRemoveDiscountAsync(productsids));
                _cacheHelper.ClearProductCache();
                _cacheHelper.ClearProductCache();
                return Result<bool>.Ok(true, "Discount deleted", 200);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error in DeleteDiscountAsync for id: {id}");
                _discountBackgroundJopMethod.NotifyAdminError($"Error in DeleteDiscountAsync for id {id}: {ex.Message}", ex.StackTrace);
                return Result<bool>.Fail("Error deleting discount", 500);
            }
        }

        public async Task<Result<DiscountDto>> RestoreDiscountAsync(int id, string userId)
        {
            _logger.LogInformation($"Restoring discount: {id}");
            try
            {
                var discount = await _unitOfWork.Repository<Models.Discount>().GetByIdAsync(id);
                if (discount == null)
                    return Result<DiscountDto>.Fail("Discount not found", 404);

                using var transaction = await _unitOfWork.BeginTransactionAsync();

                discount.DeletedAt = null;
                var result = _unitOfWork.Repository<Models.Discount>().Update(discount);
                if (!result)
                {
                    await transaction.RollbackAsync();
                    return Result<DiscountDto>.Fail("Failed to restore discount", 400);
                }

                await _adminOperationServices.AddAdminOpreationAsync(
                    $"Restore Discount {id}",
                    Opreations.UpdateOpreation,
                    userId,
                    id
                );

                await _unitOfWork.CommitAsync();
                await transaction.CommitAsync();
                _cacheHelper.ClearProductCache();

                var discountDto = new DiscountDto
                {
                    Id = discount.Id,
                    Name = discount.Name,
                    Description = discount.Description,
                    DiscountPercent = discount.DiscountPercent,
                    StartDate = discount.StartDate,
                    EndDate = discount.EndDate,
                    IsActive = discount.IsActive,
                    CreatedAt = discount.CreatedAt,
                    ModifiedAt = discount.ModifiedAt
                };

                return Result<DiscountDto>.Ok(discountDto, "Discount restored successfully", 200);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error in RestoreDiscountAsync for id: {id}");
                _discountBackgroundJopMethod.NotifyAdminError($"Error in RestoreDiscountAsync for id {id}: {ex.Message}", ex.StackTrace);
                return Result<DiscountDto>.Fail("Error restoring discount", 500);
            }
        }

        public async Task<Result<bool>> ActivateDiscountAsync(int id, string userId)
        {
            _logger.LogInformation($"Activating discount: {id}");
            try
            {
                var discount = await _unitOfWork.Repository<Models.Discount>().GetByIdAsync(id);
                if (discount == null)
                    return Result<bool>.Fail("Discount not found", 404);

                if (discount.IsActive)
                    return Result<bool>.Fail("Discount is already active", 400);

                var now = DateTime.UtcNow;

                if (discount.EndDate < now)
                    return Result<bool>.Fail("Cannot activate a discount that has already expired", 400);

                if (discount.StartDate > now)
                    return Result<bool>.Fail($"Cannot activate a discount that Start Time {discount.StartDate}", 400);

                using var transaction = await _unitOfWork.BeginTransactionAsync();

                discount.IsActive = true;
                var result = _unitOfWork.Repository<Models.Discount>().Update(discount);
                if (!result)
                {
                    await transaction.RollbackAsync();
                    return Result<bool>.Fail("Failed to activate discount", 400);
                }

                await _adminOperationServices.AddAdminOpreationAsync(
                     $"Activated discount '{discount.Name}' (ID: {discount.Id})",
                     Opreations.UpdateOpreation,
                    userId,
                    discount.Id
                );
                var productsids = await _unitOfWork.Product.GetAll().Where(p => p.DiscountId == id && p.DeletedAt == null).Select(p => p.Id).ToListAsync();

                _backgroundJobClient.Enqueue(() => _cartServices.UpdateCartItemsForProductsAfterAddDiscountAsync(productsids, discount.DiscountPercent));
                await _unitOfWork.CommitAsync();
                await transaction.CommitAsync();
                _cacheHelper.ClearProductCache();
                return Result<bool>.Ok(true, "Discount activated successfully", 200);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error in ActivateDiscountAsync for id: {id}");
                _discountBackgroundJopMethod.NotifyAdminError($"Error in ActivateDiscountAsync for id {id}: {ex.Message}", ex.StackTrace);
                return Result<bool>.Fail("Error activating discount", 500);
            }
        }

        public async Task<Result<bool>> DeactivateDiscountAsync(int id, string userId)
        {
            _logger.LogInformation($"Deactivating discount: {id}");

            try
            {
                var discount = await _unitOfWork.Repository<Models.Discount>().GetByIdAsync(id);
                if (discount == null)
                    return Result<bool>.Fail("Discount not found", 404);

                if (!discount.IsActive)
                    return Result<bool>.Fail("Discount is already inactive", 400);

                using var transaction = await _unitOfWork.BeginTransactionAsync();

                discount.IsActive = false;
                var result = _unitOfWork.Repository<Models.Discount>().Update(discount);

                if (!result)
                {
                    await transaction.RollbackAsync();
                    return Result<bool>.Fail("Failed to deactivate discount", 400);
                }

                await _adminOperationServices.AddAdminOpreationAsync(
                    description: $"Deactivated discount '{discount.Name}' (ID: {discount.Id})",
                    opreation: Opreations.UpdateOpreation,
                     userid: userId,
                    itemid: discount.Id
                );

                var productsids = await _unitOfWork.Product.GetAll().Where(p => p.DiscountId == id && p.DeletedAt == null).Select(p => p.Id).ToListAsync();
                _backgroundJobClient.Enqueue(() => _cartServices.UpdateCartItemsForProductsAfterRemoveDiscountAsync(productsids));
                await _unitOfWork.CommitAsync();
                await transaction.CommitAsync();
                _cacheHelper.ClearProductCache();

                return Result<bool>.Ok(true, "Discount deactivated successfully", 200);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error in DeactivateDiscountAsync for id: {id}");
                _discountBackgroundJopMethod.NotifyAdminError($"Error in DeactivateDiscountAsync for id {id}: {ex.Message}", ex.StackTrace);

                return Result<bool>.Fail("Error deactivating discount", 500);
            }
        }

        public async Task<Result<bool>> UpdateCartPricesOnDiscountChange(int discountId)
        {
            _logger.LogInformation($"Updating cart prices for discount: {discountId}");
            try
            {
                var discount = await _unitOfWork.Repository<Models.Discount>().GetByIdAsync(discountId);
                if (discount == null)
                    return Result<bool>.Fail("Discount not found", 404);

                var cartItemsToUpdate = await _unitOfWork.Repository<CartItem>().GetAll()
                    .Include(ci => ci.Product)
                    .ThenInclude(p => p.Discount)
                    .Where(ci => ci.Product.DiscountId == discountId && ci.Product.DeletedAt == null)
                    .ToListAsync();

                if (!cartItemsToUpdate.Any())
                {
                    _logger.LogInformation($"No cart items found with discount {discountId}");
                    return Result<bool>.Ok(true, "No cart items to update", 200);
                }

                using var transaction = await _unitOfWork.BeginTransactionAsync();
                var updatedCount = 0;

                foreach (var cartItem in cartItemsToUpdate)
                {
                    var originalPrice = cartItem.Product.Price;
                    decimal newUnitPrice;

                    var now = DateTime.UtcNow;
                    var isDiscountValid = discount.IsActive &&
                                         discount.StartDate <= now &&
                                         discount.EndDate >= now &&
                                         discount.DeletedAt == null;

                    if (isDiscountValid)
                    {
                        var discountAmount = originalPrice * (discount.DiscountPercent / 100m);
                        newUnitPrice = originalPrice - discountAmount;
                        _logger.LogInformation($"Applying discount to cart item {cartItem.Id}: {originalPrice} -> {newUnitPrice}");
                    }
                    else
                    {
                        newUnitPrice = originalPrice;
                        _logger.LogInformation($"Removing discount from cart item {cartItem.Id}: {cartItem.UnitPrice} -> {newUnitPrice}");
                    }

                    cartItem.UnitPrice = newUnitPrice;
                    updatedCount++;
                }

                await _unitOfWork.CommitAsync();
                await transaction.CommitAsync();

                _logger.LogInformation($"Successfully updated {updatedCount} cart items for discount {discountId}");
                return Result<bool>.Ok(true, $"Updated {updatedCount} cart items", 200);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error in UpdateCartPricesOnDiscountChange for discountId: {discountId}");
                _discountBackgroundJopMethod.NotifyAdminError($"Error in UpdateCartPricesOnDiscountChange for discountId {discountId}: {ex.Message}", ex.StackTrace);
                return Result<bool>.Fail("Error updating cart prices", 500);
            }
        }


    }
}
