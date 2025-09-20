using E_Commerce.DtoModels.SubCategorydto;
using E_Commerce.Enums;
using E_Commerce.Interfaces;
using E_Commerce.Models;
using E_Commerce.Services;
using E_Commerce.Services.AdminOperationServices;
using E_Commerce.Services.Cache;
using E_Commerce.Services.CategoryServcies;
using E_Commerce.Services.CategoryServices;
using E_Commerce.Services.EmailServices;
using E_Commerce.UOW;
using Hangfire;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace E_Commerce.Services.SubCategoryServices
{
    public class SubCategoryCommandService : ISubCategoryCommandService
    {
        private readonly IUnitOfWork _unitOfWork;
		private readonly ICategoryCacheHelper _categoryCacheHelper;
		private readonly ILogger<SubCategoryCommandService> _logger;
        private readonly IAdminOpreationServices _adminopreationservices;
        private readonly ICacheManager _cacheManager;
        private readonly IBackgroundJobClient _backgroundJobClient;
        private readonly ISubCategoryCacheHelper _subCategoryCacheHelper;
        private readonly ISubCategoryMapper _subCategoryMapper;
		private readonly ICategoryCommandService _categoryCommandService;

		public SubCategoryCommandService(
			ICategoryCacheHelper categoryCacheHelper,
			ICategoryCommandService categoryCommandService,
			IUnitOfWork unitOfWork,
            ILogger<SubCategoryCommandService> logger,
            IAdminOpreationServices adminopreationservices,
            ICacheManager cacheManager,
            IBackgroundJobClient backgroundJobClient,
            ISubCategoryCacheHelper subCategoryCacheHelper,
            ISubCategoryMapper subCategoryMapper)
        {
			_categoryCacheHelper= categoryCacheHelper;
			_subCategoryMapper = subCategoryMapper;
			_categoryCommandService = categoryCommandService;
			_unitOfWork = unitOfWork;
            _logger = logger;
            _adminopreationservices = adminopreationservices;
            _cacheManager = cacheManager;
            _backgroundJobClient = backgroundJobClient;
            _subCategoryCacheHelper = subCategoryCacheHelper;
            _subCategoryMapper = subCategoryMapper;
        }

        
        
		public async Task<Result<SubCategoryDto>> CreateAsync(CreateSubCategoryDto subCategory, string userid)
		{
			_logger.LogInformation($"Execute {nameof(CreateAsync)}");
			if (string.IsNullOrWhiteSpace(subCategory.Name))
			{
				return Result<SubCategoryDto>.Fail("SubCategory name cannot be empty", 400);
			}


			var category = await _unitOfWork.Category.IsExsistAsync(subCategory.CategoryId);
			if (!category)
			{
				return Result<SubCategoryDto>.Fail($"Category with id {subCategory.CategoryId} not found", 404);
			}

			var isexsist = await _unitOfWork.SubCategory.IsExsistByNameAsync(subCategory.Name);
			if (isexsist)
			{
				return Result<SubCategoryDto>.Fail($"there's subcategory with this name:{subCategory.Name}", 409);
			}
			using var transaction = await _unitOfWork.BeginTransactionAsync();
			try
			{
				SubCategory subCategoryEntity = new SubCategory
				{
					CategoryId = subCategory.CategoryId,
					Name = subCategory.Name,
					IsActive = false,
					Description = subCategory.Description,


				};
				var creationResult = await _unitOfWork.SubCategory.CreateAsync(subCategoryEntity);
				if (creationResult == null)
				{
					_logger.LogWarning("Failed to create subcategory");
					NotifyAdminOfError($"Failed to create subcategory '{subCategory.Name}'");
					await transaction.RollbackAsync();
					return Result<SubCategoryDto>.Fail("Can't create subcategory now... try again later", 500);
				}
				await _unitOfWork.CommitAsync();


				var adminLog = await _adminopreationservices.AddAdminOpreationAsync(
					"Add SubCategory",
					Opreations.AddOpreation,
					userid,
					subCategoryEntity.Id
				);
				if (!adminLog.Success)
				{
					_logger.LogError(adminLog.Message);
					NotifyAdminOfError($"Failed to log admin operation for subcategory '{subCategory.Name}' (ID: {subCategoryEntity.Id})");
					await transaction.RollbackAsync();
					return Result<SubCategoryDto>.Fail("Try Again later", 500);
				}
				_subCategoryCacheHelper.ClearSubCategoryCache();
				_categoryCacheHelper.ClearCategoryDataCache();
				await transaction.CommitAsync();
				var subcategorydto = _subCategoryMapper.ToSubCategoryDto(creationResult);

				_logger.LogInformation($"Successfully mapped subcategory to DTO");

				return Result<SubCategoryDto>.Ok(subcategorydto, "Created", 201);

			}
			catch (Exception ex)
			{
				await transaction.RollbackAsync();
				_logger.LogError($"❌ Exception in CreateAsync: {ex.Message}");
				NotifyAdminOfError($"Exception in CreateAsync for subcategory '{subCategory.Name}': {ex.Message}", ex.StackTrace);
				return Result<SubCategoryDto>.Fail("Can't create subcategory now... try again later", 500);
			}
		}

		public async Task<Result<bool>> DeleteAsync(int id, string userid)
		{
			_logger.LogInformation($"Executing {nameof(DeleteAsync)} for subCategoryId: {id}");

			using var transaction = await _unitOfWork.BeginTransactionAsync();
			try
			{

				var subcategoryinfo = await _unitOfWork.SubCategory.GetAll().Where(c => c.Id == id).Select(x => new
				{
					isdelete = x.DeletedAt != null,
					ishasproducts = x.Products.Any(),

				}).FirstOrDefaultAsync();


				if (subcategoryinfo == null)
				{
					await transaction.RollbackAsync();
					return Result<bool>.Fail($"SubCategory with id {id} not found", 404);
				}

				if (subcategoryinfo.isdelete)
				{
					_logger.LogWarning($"SubCategory {id} is already deleted");
					return Result<bool>.Fail($"SubCategory with id {id} is already deleted", 400);
				}


				if (subcategoryinfo.ishasproducts)
				{
					await transaction.RollbackAsync();
					_logger.LogWarning($"SubCategory {id} contains products");
					return Result<bool>.Fail("Can't delete subcategory because it has products", 400);
				}
				var deleteResult = await _unitOfWork.SubCategory.SoftDeleteAsync(id);
				if (!deleteResult)
				{
					await transaction.RollbackAsync();
					return Result<bool>.Fail($"Failed to delete subcategory", 500);
				}

				var adminLog = await _adminopreationservices.AddAdminOpreationAsync(
					$"Deleted SubCategory {id}",
					Opreations.DeleteOpreation,
					userid,
					id
				);

				if (!adminLog.Success)
				{
					_logger.LogWarning($"Failed to log admin operation: {adminLog.Message}");
					await transaction.RollbackAsync();
					return Result<bool>.Fail("Failed to log admin operation", 500);
				}

				await _unitOfWork.CommitAsync();
				await transaction.CommitAsync();
				_subCategoryCacheHelper.ClearSubCategoryCache();
				_categoryCacheHelper.ClearCategoryDataCache();

				return Result<bool>.Ok(true, $"SubCategory with ID {id} deleted successfully", 200);
			}
			catch (Exception ex)
			{
				await transaction.RollbackAsync();
				_logger.LogError($"Exception in DeleteAsync: {ex.Message}");
				NotifyAdminOfError($"Exception in DeleteAsync: {ex.Message}", ex.StackTrace);
				return Result<bool>.Fail("An error occurred while deleting subcategory", 500);
			}
		}


		public async Task<Result<bool>> ActivateSubCategoryAsync(int subCategoryId, string userId)
		{
			using var transaction = await _unitOfWork.BeginTransactionAsync();
			try
			{


				_logger.LogInformation($"Activating subcategory {subCategoryId}");

				var subcategoryInfo = await _unitOfWork.SubCategory.GetAll()
					.Where(c => c.Id == subCategoryId && c.DeletedAt == null)
					.Select(c => new
					{
						IsActive = c.IsActive,
						HasActiveProduct = c.Products.Any(sc => sc.IsActive && sc.DeletedAt == null),
						HasImages = c.Images.Any(i => i.DeletedAt == null)
					})
					.FirstOrDefaultAsync();

				if (subcategoryInfo == null)
					return Result<bool>.Fail($"SubCategory with id {subCategoryId} not found", 404);

				if (!subcategoryInfo.HasImages)
					return Result<bool>.Fail("Cannot activate subcategory without at least one image", 400);

				if (!subcategoryInfo.HasActiveProduct)
				{
					return Result<bool>.Fail("Cannot activate subcategory with Inactive products", 400);
				}

				var updateResult = await _unitOfWork.SubCategory.ActiveSubCategoryAsync(subCategoryId);
				if (!updateResult)

				{
					_logger.LogWarning("SubCategory Maybe Active Or Not found");
					await transaction.RollbackAsync();
					return Result<bool>.Fail(" subcategory is already Active", 400);
				}
				await _unitOfWork.CommitAsync();
				await transaction.CommitAsync();
				_subCategoryCacheHelper.ClearSubCategoryCache();
				_categoryCacheHelper.ClearCategoryDataCache();
				return Result<bool>.Ok(true, "SubCategory activated successfully", 200);
			}
			catch (Exception ex)
			{
				_logger.LogError(ex.Message);
				NotifyAdminOfError(ex.Message, ex.StackTrace);
				return Result<bool>.Fail("Try Again Later", 500);

			}

		}

		public async Task<Result<bool>> DeactivateSubCategoryAsync(int subCategoryId, string userId)
		{
			using var transaction = await _unitOfWork.BeginTransactionAsync();
			try
			{
				var subcategoryInfo = await _unitOfWork.SubCategory.GetAll()
					.Where(c => c.Id == subCategoryId && c.DeletedAt == null)
					.Select(c => new
					{
						IsActive = c.IsActive,
						HasActiveSubCategories = c.Products.Any(sc => sc.IsActive && sc.DeletedAt == null),
						HasImages = c.Images.Any(i => i.DeletedAt == null),
						CategoryId = c.CategoryId
					})
					.FirstOrDefaultAsync();
				_logger.LogInformation($"Deactivating subcategory {subCategoryId}");


				if (subcategoryInfo == null)
					return Result<bool>.Fail($"SubCategory with id {subCategoryId} not found", 404);

				if (!subcategoryInfo.IsActive)
				{
					_logger.LogInformation($"SubCategory {subCategoryId} is already inactive. No action taken.");
					return Result<bool>.Fail("SubCategory is already inactive", 400);
				}




				var updateResult = await _unitOfWork.SubCategory.DeActiveSubCategoryAsync(subCategoryId);
				if (!updateResult)
				{
					await transaction.RollbackAsync();
					_logger.LogWarning("Failed to deactivate subcategory. DB update returned false.");
					return Result<bool>.Fail("Failed to deactivate subcategory", 500);
				}

				await _unitOfWork.CommitAsync();
				await transaction.CommitAsync();

				_subCategoryCacheHelper.ClearSubCategoryCache();
				_categoryCacheHelper.ClearCategoryDataCache();


				_backgroundJobClient.Enqueue(() => _categoryCommandService.DeactivateCategoryIfNoActiveSubcategories(subcategoryInfo.CategoryId, userId));

				_logger.LogInformation($"✅ Subcategory {subCategoryId} deactivated successfully.");
				return Result<bool>.Ok(true, "SubCategory deactivated successfully", 200);
			}
			catch (Exception ex)
			{
				await transaction.RollbackAsync();
				_logger.LogError(ex, $"❌ Error in DeactivateSubCategoryAsync for subCategoryId: {subCategoryId}");
				NotifyAdminOfError($"Exception in DeactivateSubCategoryAsync for subcategory {subCategoryId}: {ex.Message}", ex.StackTrace);
				return Result<bool>.Fail("An error occurred while deactivating subcategory", 500);
			}
		}

		public async Task DeactivateSubCategoryIfAllProductsAreInactiveAsync(int subCategoryId, string userId)
		{


			using var transaction = await _unitOfWork.BeginTransactionAsync();
			try
			{

				var checkonsubcategory = await _unitOfWork.SubCategory.GetAll().Select(sc=> new 
				{
					hasactiveporduct = sc.Products.Any(p=>p.IsActive && p.DeletedAt == null),
					isactive = sc.IsActive,
					subCategoryId = sc.Id,
					CategoryId = sc.CategoryId,
					isdeleted=sc.DeletedAt!= null
				} 
				).FirstOrDefaultAsync(sc=>sc.subCategoryId==subCategoryId&&!sc.isdeleted);
				_logger.LogInformation($"Checking if subcategory {subCategoryId} needs to be deactivated.");

				if(checkonsubcategory == null)
				{
					_logger.LogWarning($"SubCategory {subCategoryId} not found or is deleted.");
					return;
				}

				if (checkonsubcategory.hasactiveporduct)
				{
					_logger.LogInformation($"Subcategory {subCategoryId} still has active products. No action taken.");
					return;
				}

			
				if (!checkonsubcategory.isactive)
				{
					_logger.LogWarning($"SubCategory {subCategoryId} is already inactive.");
					return;
				}
				if(checkonsubcategory.isdeleted)
				{
					_logger.LogWarning($"SubCategory {subCategoryId} is deleted.");
				
					return;
				}
				await _adminopreationservices.AddAdminOpreationAsync(
					$"Deactivated SubCategory {subCategoryId} because all its products became inactive.",
					Opreations.UpdateOpreation,
					userId,
					subCategoryId
				);

				await _unitOfWork.CommitAsync();
				await transaction.CommitAsync();

				_subCategoryCacheHelper.ClearSubCategoryCache();
				_categoryCacheHelper.ClearCategoryDataCache();

				_backgroundJobClient.Enqueue(() => _categoryCommandService.DeactivateCategoryIfNoActiveSubcategories(checkonsubcategory.CategoryId, userId));
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, $"Error in DeactivateSubCategoryIfAllProductsAreInactiveAsync for subCategoryId: {subCategoryId}");
				await transaction.RollbackAsync();
				NotifyAdminOfError($"Exception in DeactivateSubCategoryIfAllProductsAreInactiveAsync for subcategory {subCategoryId}: {ex.Message}", ex.StackTrace);
				throw;
			}
		}
		public async Task<Result<SubCategoryDto>> ReturnRemovedSubCategoryAsync(int id, string userid)
		{
			_logger.LogInformation($"Executing {nameof(ReturnRemovedSubCategoryAsync)} for id: {id}");

			using var transaction = await _unitOfWork.BeginTransactionAsync();
			try
			{
				var subCategory = await _unitOfWork.SubCategory.GetByIdAsync(id);
				if (subCategory == null)
				{
					await transaction.RollbackAsync();
					_logger.LogWarning($"Can't Found SubCategory with this id:{id}");
					return Result<SubCategoryDto>.Fail($"Can't Found SubCategory with this id:{id}", 404);
				}

				var restoreResult = await _unitOfWork.SubCategory.RestoreAsync(id);
				if (!restoreResult)
				{
					await transaction.RollbackAsync();
					return Result<SubCategoryDto>.Fail("Try Again later", 500);
				}

				var adminLog = await _adminopreationservices.AddAdminOpreationAsync(
					$"Restored SubCategory {id}",
					Opreations.UpdateOpreation,
					userid,
					id
				);

				if (!adminLog.Success)
				{
					_logger.LogWarning($"Failed to log admin operation: {adminLog.Message}");
					await transaction.RollbackAsync();
					return Result<SubCategoryDto>.Fail("Failed to log admin operation", 500);
				}

				await _unitOfWork.CommitAsync();
				await transaction.CommitAsync();
				_subCategoryCacheHelper.ClearSubCategoryCache();
				_categoryCacheHelper.ClearCategoryDataCache();

				var dto = _subCategoryMapper.MapToSubCategoryDtoWithData(subCategory);
				return Result<SubCategoryDto>.Ok(dto, "SubCategory restored successfully", 200);
			}
			catch (Exception ex)
			{
				await transaction.RollbackAsync();
				_logger.LogError($"Exception in ReturnRemovedSubCategoryAsync: {ex.Message}");
				NotifyAdminOfError($"Exception in ReturnRemovedSubCategoryAsync: {ex.Message}", ex.StackTrace);
				return Result<SubCategoryDto>.Fail("An error occurred while restoring subcategory", 500);
			}
		}

		public async Task<Result<SubCategoryDto>> UpdateAsync(int subCategoryId, UpdateSubCategoryDto subCategory, string userid)
		{
			_logger.LogInformation($"Executing {nameof(UpdateAsync)} for subCategoryId: {subCategoryId}");


			var existingSubCategory = await _unitOfWork.SubCategory.GetByIdAsync(subCategoryId);

			if (existingSubCategory == null)
			{
				return Result<SubCategoryDto>.Fail($"SubCategory with id {subCategoryId} not found", 404);
			}

			using var transaction = await _unitOfWork.BeginTransactionAsync();
			try
			{
				var warnings = new List<string>();

				if (subCategory == null)
				{
					return Result<SubCategoryDto>.Fail("Update data is required", 400);
				}

				_logger.LogInformation($"Update data received - Name: '{subCategory.Name}', Description: '{subCategory.Description}', CategoryId: {subCategory.CategoryId}");

				if (!string.IsNullOrWhiteSpace(subCategory.Name?.Trim()) && subCategory.Name.Trim() != existingSubCategory.Name)
				{
					var trimmedName = subCategory.Name.Trim();
					var nameRegex = new System.Text.RegularExpressions.Regex(@"^[a-zA-Z0-9][a-zA-Z0-9\s\-,]*[a-zA-Z0-9]$");
					if (!nameRegex.IsMatch(trimmedName))
					{
						warnings.Add($"Name '{trimmedName}' does not match the required format. Name will not be changed.");
						_logger.LogWarning($"Name update skipped - invalid format '{trimmedName}'");
					}
					else if (trimmedName.Length < 5 || trimmedName.Length > 20)
					{
						warnings.Add($"Name '{trimmedName}' must be between 5 and 20 characters. Name will not be changed.");
						_logger.LogWarning($"Name update skipped - invalid length '{trimmedName}'");
					}
					else
					{
						_logger.LogInformation($"Updating name from '{existingSubCategory.Name}' to '{trimmedName}'");
						var isexist = await _unitOfWork.SubCategory.IsExsistByNameAsync(subCategory.Name);


						if (isexist)
						{
							warnings.Add($"SubCategory with name '{trimmedName}' already exists. Name will not be changed.");
							_logger.LogWarning($"Name update skipped - duplicate name '{trimmedName}'");
						}
						else
						{
							existingSubCategory.Name = trimmedName;
							_logger.LogInformation($"Name updated successfully to '{trimmedName}'");
						}
					}
				}


				if (subCategory.CategoryId.HasValue && subCategory.CategoryId.Value != existingSubCategory.CategoryId)
				{
					_logger.LogInformation($"Updating CategoryId from {existingSubCategory.CategoryId} to {subCategory.CategoryId.Value}");
					var category = await _unitOfWork.Category.GetByIdAsync(subCategory.CategoryId.Value);
					if (category == null)
					{
						warnings.Add($"Category with id {subCategory.CategoryId.Value} not found. Category will not be changed.");
						_logger.LogWarning($"Category update skipped - category {subCategory.CategoryId.Value} not found");
					}
					else
					{
						existingSubCategory.CategoryId = subCategory.CategoryId.Value;
						_logger.LogInformation($"CategoryId updated successfully to {subCategory.CategoryId.Value}");
					}
				}

				if (!string.IsNullOrWhiteSpace(subCategory.Description?.Trim()) && subCategory.Description.Trim() != existingSubCategory.Description)
				{
					var trimmedDescription = subCategory.Description.Trim();

					var descRegex = new System.Text.RegularExpressions.Regex(@"^[\w\s.,\-()'\""]{0,500}$");
					if (!descRegex.IsMatch(trimmedDescription))
					{
						warnings.Add($"Description '{trimmedDescription}' does not match the required format. Description will not be changed.");
						_logger.LogWarning($"Description update skipped - invalid format '{trimmedDescription}'");
					}
					else if (trimmedDescription.Length < 10 || trimmedDescription.Length > 50)
					{
						warnings.Add($"Description '{trimmedDescription}' must be between 10 and 50 characters. Description will not be changed.");
						_logger.LogWarning($"Description update skipped - invalid length '{trimmedDescription}'");
					}
					else
					{
						_logger.LogInformation($"Updating description from '{existingSubCategory.Description}' to '{trimmedDescription}'");
						existingSubCategory.Description = trimmedDescription;
			
						_logger.LogInformation("Description updated successfully");
					}
				}




			
				
					existingSubCategory.ModifiedAt = DateTime.UtcNow;
					_logger.LogInformation($"SubCategory {subCategoryId} has changes, updating ModifiedAt timestamp");
					_logger.LogInformation($"Final entity state - Name: '{existingSubCategory.Name}', Description: '{existingSubCategory.Description}', CategoryId: {existingSubCategory.CategoryId}, IsActive: {existingSubCategory.IsActive}, ModifiedAt: {existingSubCategory.ModifiedAt}");


				

				var adminLog = await _adminopreationservices.AddAdminOpreationAsync(
					$"Updated SubCategory {subCategoryId}",
					Opreations.UpdateOpreation,
					userid,
					subCategoryId
				);

				if (!adminLog.Success)
				{
					_logger.LogWarning($"Failed to log admin operation: {adminLog.Message}");
					await transaction.RollbackAsync();
					return Result<SubCategoryDto>.Fail("Failed to log admin operation", 500);
				}
				await _unitOfWork.CommitAsync();
				await transaction.CommitAsync();
				_subCategoryCacheHelper.ClearSubCategoryCache();
				_categoryCacheHelper.ClearCategoryDataCache();
				_logger.LogInformation($"Successfully updated SubCategory {subCategoryId}");
				var dto = _subCategoryMapper.MapToSubCategoryDtoWithData(existingSubCategory);
				return Result<SubCategoryDto>.Ok(dto, "Updated", 200, warnings: warnings);
			}
			catch (Exception ex)
			{
				await transaction.RollbackAsync();
				_logger.LogError($"Exception in UpdateAsync: {ex.Message}");
				NotifyAdminOfError($"Exception in UpdateAsync for subcategory {subCategoryId}: {ex.Message}", ex.StackTrace);
				return Result<SubCategoryDto>.Fail("An error occurred during update", 500);
			}
		}




		public async Task<Result<bool>> DeleteSubCategoryAsync(int subCategoryId, string userId)
        {
            _logger.LogInformation($"Executing {nameof(DeleteSubCategoryAsync)} for id: {subCategoryId}");

            if (subCategoryId <= 0)
            {
                return Result<bool>.Fail("Invalid subcategory ID", 400);
            }

            using var transaction = await _unitOfWork.BeginTransactionAsync();
            try
            {
                var subCategoryToDelete = await _unitOfWork.SubCategory.GetByIdAsync(subCategoryId);
                if (subCategoryToDelete == null || subCategoryToDelete.DeletedAt != null)
                {
                    await transaction.RollbackAsync();
                    return Result<bool>.Fail($"SubCategory with ID {subCategoryId} not found or already deleted", 404);
                }

                // Check for associated products before deleting
                var hasProducts = await _unitOfWork.Product.GetAll().AnyAsync(p => p.SubCategoryId == subCategoryId && p.DeletedAt == null);
                if (hasProducts)
                {
                    await transaction.RollbackAsync();
                    return Result<bool>.Fail("Cannot delete subcategory with associated products", 400);
                }

                var deleteResult = await _unitOfWork.SubCategory.SoftDeleteAsync(subCategoryId);
                if (!deleteResult)
                {
                    await transaction.RollbackAsync();
                    _logger.LogError($"Failed to soft delete subcategory {subCategoryId}");
                    NotifyAdminOfError($"Failed to soft delete subcategory {subCategoryId}");
                    return Result<bool>.Fail("Failed to delete subcategory", 500);
                }

                var adminLog = await _adminopreationservices.AddAdminOpreationAsync(
                    $"Deleted SubCategory {subCategoryId}",
                    Opreations.DeleteOpreation,
                    userId,
                    subCategoryId
                );

                if (!adminLog.Success)
                {
                    _logger.LogError(adminLog.Message);
                    await transaction.RollbackAsync();
                    NotifyAdminOfError($"Failed to log admin operation for subcategory ID: {subCategoryId}");
                    return Result<bool>.Fail("Try Again later", 500);
                }

                _subCategoryCacheHelper.ClearSubCategoryCache();
				_categoryCacheHelper.ClearCategoryDataCache();
				await _unitOfWork.CommitAsync();
                await transaction.CommitAsync();

                return Result<bool>.Ok(true, $"SubCategory with ID {subCategoryId} deleted successfully", 200);
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, $"Exception in DeleteSubCategoryAsync: {ex.Message}");
                NotifyAdminOfError($"Exception in DeleteSubCategoryAsync: {ex.Message}", ex.StackTrace);
                return Result<bool>.Fail("Server error occurred while deleting subcategory", 500);
            }
        }

        private void NotifyAdminOfError(string message, string? stackTrace = null)
        {
            _backgroundJobClient.Enqueue<IErrorNotificationService>(_ => _.SendErrorNotificationAsync(message, stackTrace));
        }
    }
}