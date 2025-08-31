using E_Commerce.DtoModels.CategoryDtos;
using E_Commerce.DtoModels.ImagesDtos;
using E_Commerce.DtoModels.SubCategorydto;
using E_Commerce.Enums;
using E_Commerce.Models;
using E_Commerce.Services.AdminOperationServices;
using E_Commerce.Services.Cache;
using E_Commerce.Services.EmailServices;
using E_Commerce.UOW;
using Hangfire;
using Microsoft.EntityFrameworkCore;

namespace E_Commerce.Services.CategoryServices
{
	public class CategoryCommandService : ICategoryCommandService
	{
		private readonly IUnitOfWork _unitOfWork;
		private readonly ILogger<CategoryCommandService> _logger;
		private readonly IAdminOpreationServices _adminopreationservices;
		private readonly ICacheManager _cacheManager;
		private readonly IBackgroundJobClient _backgroundJobClient ;
		private readonly ICategoryCacheHelper _categoryCacheHelper;
		private readonly ICategoryMapper _categoryMapper;


		public CategoryCommandService(
			ICategoryMapper categoryMapper,
			ICategoryCacheHelper categoryCacheHelper,
			 IBackgroundJobClient backgroundJobClient,
			IUnitOfWork unitOfWork,
			ILogger<CategoryCommandService> logger,
			IAdminOpreationServices adminopreationservices,
			ICacheManager cacheManager
			)
		{
			_categoryMapper = categoryMapper;
			_cacheManager = cacheManager;
			_categoryCacheHelper = categoryCacheHelper;
			_backgroundJobClient = backgroundJobClient;
			_unitOfWork = unitOfWork;
			_logger = logger;
			_adminopreationservices = adminopreationservices;
			_cacheManager = cacheManager;

		}
		public async Task DeactivateCategoryIfNoActiveSubcategories(int categoryId, string userId)
		{
			var check = await _unitOfWork.Category.HasSubCategoriesActiveAsync(categoryId);

			if (!check)
			{
				_logger.LogInformation($"All subcategories for category {categoryId} are inactive. Deactivating category.");
				await  DeactivateAsync(categoryId,userId);
			}
		}
		public async Task<Result<bool>> ActivateAsync(int id, string userId)
		{
		
			using var transaction = await _unitOfWork.BeginTransactionAsync();
			_logger.LogInformation($"[ActivateCategory] Start activation for id: {id}");

			try
			{
				var categoryInfo = await _unitOfWork.Category.GetAll()
					.Where(c => c.Id == id && c.DeletedAt == null)
					.Select(c => new
					{
						IsActive = c.IsActive,
						HasActiveSubCategories = c.SubCategories.Any(sc => sc.IsActive && sc.DeletedAt == null),
						HasImages = c.Images.Any(i => i.DeletedAt == null)
					})
					.FirstOrDefaultAsync();

				if (categoryInfo == null)
				{
					_logger.LogWarning($"[ActivateCategory] Category {id} not found.");
					return Result<bool>.Fail($"Category with id {id} not found", 404);
				}

				if (categoryInfo.IsActive)
				{
					_logger.LogWarning($"[ActivateCategory] Category {id} is already active.");
					return Result<bool>.Fail($"Category with id {id} is already active", 400);
				}

				if (!categoryInfo.HasImages)
				{
					_logger.LogWarning($"[ActivateCategory] Category {id} has no active images.");
					return Result<bool>.Fail("Cannot activate category without at least one image", 400);
				}

				if (!categoryInfo.HasActiveSubCategories)
				{
					_logger.LogWarning($"[ActivateCategory] Category {id} has no active subcategories.");
					return Result<bool>.Fail("Cannot activate category without at least one active subcategory", 400);
				}

				if (!await _unitOfWork.Category.ActiveCategoryAsync(id))
				{
					_logger.LogError($"[ActivateCategory] Failed to activate category {id}");
					return Result<bool>.Fail("Failed to activate category", 500);
				}

				var adminOpResult = await _adminopreationservices.AddAdminOpreationAsync(
					"Activate Category",
					Opreations.UpdateOpreation,
					userId,
					id
				);

				if (!adminOpResult.Success)
				{
					await transaction.RollbackAsync();
					_logger.LogError($"[ActivateCategory] Failed to log admin operation: {adminOpResult.Message}");
					return Result<bool>.Fail("An error occurred while logging admin operation", 500);
				}

				await _unitOfWork.CommitAsync();
				await transaction.CommitAsync();
				_categoryCacheHelper.ClearCategoryCache();

				_logger.LogInformation($"[ActivateCategory] Category {id} activated successfully.");
				return Result<bool>.Ok(true, "Category activated successfully", 200);
			}
			catch (Exception ex)
			{
				await transaction.RollbackAsync();

				var errorMessage = $"Exception in ActivateCategoryAsync: {ex.Message}";
				NotifyAdminOfError(errorMessage, ex.StackTrace);
				_logger.LogError(ex, $"[ActivateCategory] {errorMessage}");

				return Result<bool>.Fail("An unexpected error occurred while activating the category", 500);
			}
		}

		private void NotifyAdminOfError(string message, string? stackTrace = null)
		{
			_backgroundJobClient.Enqueue<IErrorNotificationService>(_ => _.SendErrorNotificationAsync(message, stackTrace));
		}


		public async Task<Result<CategoryDto>> CreateAsync(CreateCategotyDto model, string userId)
		{
			_logger.LogInformation($"Execute {nameof(CreateAsync)}");

			if (model == null)
			{
				return Result<CategoryDto>.Fail("Category model cannot be null", 400);
			}

			if (string.IsNullOrWhiteSpace(model.Name))
			{
				return Result<CategoryDto>.Fail("Category name cannot be empty", 400);
			}

			if (string.IsNullOrWhiteSpace(userId))
			{
				return Result<CategoryDto>.Fail("User ID cannot be empty", 400);
			}
			var isexsist = await _unitOfWork.Category.IsExsistsByNameAsync(model.Name);
			if (isexsist)
			{
				return Result<CategoryDto>.Fail($"There's a category with this name: {model.Name}", 409);
			}

			using var transaction = await _unitOfWork.BeginTransactionAsync();
			try
			{
				var category = new Category
				{
					Description = model.Description,
					Name = model.Name,
					IsActive = false,
				};
				var creationResult = await _unitOfWork.Category.CreateAsync(category);
				if (creationResult == null)
				{
					_logger.LogWarning("Failed to create category");
					NotifyAdminOfError($"Failed to create category '{model.Name}'");
					await transaction.RollbackAsync();
					return Result<CategoryDto>.Fail("Can't create category now... try again later", 500);
				}
				await _unitOfWork.CommitAsync();
				var adminLog = await _adminopreationservices.AddAdminOpreationAsync(
					"Add Category",
					Opreations.AddOpreation,
					userId,
					category.Id
				);
				if (!adminLog.Success)
				{
					_logger.LogError(adminLog.Message);
					await transaction.RollbackAsync();
					NotifyAdminOfError($"Failed to log admin operation for category '{model.Name}' (ID: {category.Id})");
					return Result<CategoryDto>.Fail("Try Again later", 500);
				}
						_categoryCacheHelper.ClearCategoryCache();
				await _unitOfWork.CommitAsync();
				await transaction.CommitAsync();
				var categoryaftercreate = await _unitOfWork.Category.GetByIdAsync(category.Id);
				if (categoryaftercreate == null)
				{
					_logger.LogError("Failed to retrieve created category");
					NotifyAdminOfError($"Failed to retrieve created category with ID {category.Id} after creation");
					return Result<CategoryDto>.Fail("Category created but failed to retrieve details", 500);
				}
				var categoryDto = _categoryMapper.ToCategoryDto(categoryaftercreate);
				return Result<CategoryDto>.Ok(categoryDto, "Created", 201);
			}
			catch (Exception ex)
			{
				await transaction.RollbackAsync();
				_logger.LogError($"Exception in CreateAsync: {ex.Message}");
				NotifyAdminOfError($"Exception in CreateAsync: {ex.Message}");
				return Result<CategoryDto>.Fail("Server error occurred while creating category", 500);
			}
		}
		public async Task<Result<bool>> DeactivateAsync(int id, string userId)
		{
			using var transaction = await _unitOfWork.BeginTransactionAsync();
			_logger.LogInformation($"[DeactivateCategory] Starting deactivation process for Category ID: {id}");

			try
			{
				var categoryInfo = await _unitOfWork.Category.GetAll()
					.Where(c => c.Id == id)
					.Select(c => new
					{
						IsActive = c.IsActive,
						DeletedAt = c.DeletedAt,
						HasActiveSubCategories = c.SubCategories.Any(sc => sc.IsActive && sc.DeletedAt == null)
					})
					.FirstOrDefaultAsync();

				if (categoryInfo == null)
				{
					_logger.LogWarning($"[DeactivateCategory] Category {id} not found.");
					return Result<bool>.Fail($"Category with ID {id} not found.", 404);
				}

				if (!categoryInfo.IsActive || categoryInfo.DeletedAt != null)
				{
					_logger.LogWarning($"[DeactivateCategory] Category {id} is already deactivated.");
					return Result<bool>.Fail($"Category with ID {id} is already deactivated.", 400);
				}

				if (categoryInfo.HasActiveSubCategories)
				{
					_logger.LogWarning($"[DeactivateCategory] Category {id} still has active subcategories.");
					return Result<bool>.Fail("Cannot deactivate category while it still has active subcategories.", 400);
				}

				if (!await _unitOfWork.Category.DeactiveCategoryAsync(id))
				{
					_logger.LogError($"[DeactivateCategory] Failed to deactivate category {id}.");
					return Result<bool>.Fail("Failed to deactivate the category.", 500);
				}

				var adminOpResult = await _adminopreationservices.AddAdminOpreationAsync(
					"Deactivate Category",
					Opreations.UpdateOpreation,
					userId,
					id
				);

				if (!adminOpResult.Success)
				{
					await transaction.RollbackAsync();
					_logger.LogWarning($"[DeactivateCategory] Failed to log admin operation: {adminOpResult.Message}");
					return Result<bool>.Fail("An error occurred while logging the admin operation.", 500);
				}

				await _unitOfWork.CommitAsync();
				await transaction.CommitAsync();
				_categoryCacheHelper.ClearCategoryCache();

				_logger.LogInformation($"[DeactivateCategory] Category {id} deactivated successfully.");
				return Result<bool>.Ok(true, "Category deactivated successfully.", 200);
			}
			catch (Exception ex)
			{
				await transaction.RollbackAsync();
				_logger.LogError(ex, $"[DeactivateCategory] Unexpected error occurred for category {id}.");
				NotifyAdminOfError($"Exception in DeactivateCategoryAsync: {ex.Message}", ex.StackTrace);
				return Result<bool>.Fail("An unexpected error occurred while deactivating the category.", 500);
			}
		}

		public async Task<Result<bool>> DeleteAsync(int id, string userId)
		{
			_logger.LogInformation($"Executing {nameof(DeleteAsync)} for id: {id}");

			if (id <= 0)
				return Result<bool>.Fail("Invalid category ID", 400);

			if (string.IsNullOrWhiteSpace(userId))
				return Result<bool>.Fail("User ID cannot be empty", 400);

			using var transaction = await _unitOfWork.BeginTransactionAsync();

			try
			{
				var categoryInfo = await _unitOfWork.Category.GetAll()
					.Where(c => c.Id == id)
					.Select(c => new
					{
						Category = c,
						IsDeleted = c.DeletedAt != null,
						HasSubCategories = c.SubCategories.Any()
					})
					.FirstOrDefaultAsync();

				if (categoryInfo == null || categoryInfo.IsDeleted)
				{
					await transaction.RollbackAsync();
					return Result<bool>.Fail($"Category with id {id} not found or already deleted", 404);
				}

				if (categoryInfo.HasSubCategories)
				{
					await transaction.RollbackAsync();
					_logger.LogWarning($"Category {id} contains subcategories");
					return Result<bool>.Fail("Can't delete category because it has subcategories", 400);
				}

				var deleteResult = await _unitOfWork.Category.SoftDeleteAsync(id);
				if (!deleteResult)
				{
					await transaction.RollbackAsync();
					return Result<bool>.Fail($"Failed to delete category", 500);
				}

				var adminLog = await _adminopreationservices.AddAdminOpreationAsync(
					$"Deleted Category {id}",
					Opreations.DeleteOpreation,
					userId,
					id
				);

				if (!adminLog.Success)
				{
					await transaction.RollbackAsync();
					_logger.LogWarning($"Failed to log admin operation: {adminLog.Message}");
					return Result<bool>.Fail("An error occurred while deleting category", 500);
				}

				await _unitOfWork.CommitAsync();
				await transaction.CommitAsync();

						_categoryCacheHelper.ClearCategoryCache();

				return Result<bool>.Ok(true, $"Category with ID {id} deleted successfully", 200);
			}
			catch (Exception ex)
			{
				await transaction.RollbackAsync();
				_logger.LogError($"Exception in DeleteAsync: {ex.Message}");
				NotifyAdminOfError($"Exception in DeleteAsync: {ex.Message}", ex.StackTrace);
				return Result<bool>.Fail("An error occurred while deleting category", 500);
			}
		}

		public async Task<Result<CategoryDto>> RestoreAsync(int id, string userid)
		{
			_logger.LogInformation($"Executing {nameof(RestoreAsync)} for id: {id}");

			if (id <= 0)
				return Result<CategoryDto>.Fail("Invalid category ID", 400);

			if (string.IsNullOrWhiteSpace(userid))
				return Result<CategoryDto>.Fail("User ID cannot be empty", 400);

			using var transaction = await _unitOfWork.BeginTransactionAsync();
			try
			{
				var category = await _unitOfWork.Category.GetByIdAsync(id);
				if (category == null || category.DeletedAt == null)
				{
					await transaction.RollbackAsync();
					_logger.LogWarning($"Can't Found Category with this id:{id}");
					return Result<CategoryDto>.Fail($"Can't Found Category with this id:{id}", 404);
				}

				var restoreResult = await _unitOfWork.Category.RestoreAsync(id);
				if (!restoreResult)
				{
					await transaction.RollbackAsync();
					return Result<CategoryDto>.Fail("Try Again later", 500);
				}

				var adminLog = await _adminopreationservices.AddAdminOpreationAsync(
					$"Restored Category {id}",
					Opreations.UpdateOpreation,
					userid,
					id
				);

				if (!adminLog.Success)
				{
					await transaction.RollbackAsync();

					_logger.LogWarning($"Failed to log admin operation: {adminLog.Message}");
					return Result<CategoryDto>.Fail("An error occurred while restoring category", 500);

				}

				await _unitOfWork.CommitAsync();
				await transaction.CommitAsync();
						_categoryCacheHelper.ClearCategoryCache();


				var restoredCategory = await GetCategoryByIdWithImagesAsync(id);
				if (restoredCategory == null)
				{
					_logger.LogError("Failed to retrieve restored category");
					return Result<CategoryDto>.Fail("Category restored but failed to retrieve details", 500);
				}

				var categorydto = _categoryMapper.ToCategoryDto(restoredCategory);
				return Result<CategoryDto>.Ok(categorydto, "Category restored successfully", 200);
			}
			catch (Exception ex)
			{
				await transaction.RollbackAsync();
				_logger.LogError($"Exception in ReturnRemovedCategoryAsync: {ex.Message}");
				NotifyAdminOfError($"Exception in ReturnRemovedCategoryAsync: {ex.Message}", ex.StackTrace);
				return Result<CategoryDto>.Fail("An error occurred while restoring category", 500);
			}
		}
		private IQueryable<E_Commerce.Models.Category> BasicFilter(IQueryable<E_Commerce.Models.Category> query, bool? isActive = null, bool? isDeleted = null)
		{
			if (isActive.HasValue)
				query = query.Where(c => c.IsActive == isActive.Value);
			if (isDeleted.HasValue)
			{
				if (isDeleted.Value)
					query = query.Where(c => c.DeletedAt != null);
				else
					query = query.Where(c => c.DeletedAt == null);
			}
			return query;
		}


		private async Task<Category?> GetCategoryByIdWithImagesAsync(int id, bool? isActive = null, bool? isDeleted = null)
		{
			_logger.LogInformation($"Executing {nameof(GetCategoryByIdWithImagesAsync)} for id: {id}");

			var query = _unitOfWork.Category.GetAll();

			query = query.Where(c => c.Id == id);

			query = BasicFilter(query, isActive, isDeleted);

			var category = await query
				.Include(c => c.Images)
				.FirstOrDefaultAsync();

			if (category == null)
			{
				_logger.LogWarning($"Category with id: {id} doesn't exist");
				return null;
			}

			_logger.LogInformation($"Category with id: {id} exists");
			return category;
		}



		public async Task<Result<CategoryDto>> UpdateAsync(int id, UpdateCategoryDto category, string userid)
		{
			_logger.LogInformation($"Executing {nameof(UpdateAsync)} for id: {id}");

			var existingCategory = await _unitOfWork.Category.GetByIdAsync(id);
			if (existingCategory == null)
			{
				return Result<CategoryDto>.Fail($"Category with id {id} not found", 404);
			}

			using var transaction = await _unitOfWork.BeginTransactionAsync();

			try
			{
				List<string> warings = new List<string>();

				if (!string.IsNullOrWhiteSpace(category.Name))
					existingCategory.Name = category.Name;

				if (!string.IsNullOrWhiteSpace(category.Description))
					existingCategory.Description = category.Description;

				if (category.DisplayOrder.HasValue)
					existingCategory.DisplayOrder = category.DisplayOrder.Value;


				var adminOpResult = await _adminopreationservices.AddAdminOpreationAsync(
					"Update Category",
					Opreations.UpdateOpreation,
					userid,
					existingCategory.Id
				);

				if (!adminOpResult.Success)
				{
					await transaction.RollbackAsync();
					_logger.LogWarning($"Failed to log admin operation: {adminOpResult.Message}");
					return Result<CategoryDto>.Fail("An error occurred during update", 500);
				}

				await _unitOfWork.CommitAsync();
				await transaction.CommitAsync();

				_categoryCacheHelper.ClearCategoryCache();

				var dto = _categoryMapper.ToCategoryDto(existingCategory);
				return Result<CategoryDto>.Ok(dto, "Updated", 200, warnings: warings);
			}

			catch (Exception ex)
			{
				await transaction.RollbackAsync();
				_logger.LogError($"Exception in UpdateAsync: {ex.Message}");
				return Result<CategoryDto>.Fail("An error occurred during update", 500);
			}

		}
	

	}
}
