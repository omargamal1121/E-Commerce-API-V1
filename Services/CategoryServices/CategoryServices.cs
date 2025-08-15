using AutoMapper;
using E_Commerce.DtoModels;
using E_Commerce.DtoModels.CategoryDtos;
using E_Commerce.DtoModels.ImagesDtos;
using E_Commerce.DtoModels.ProductDtos;
using E_Commerce.DtoModels.Responses;
using E_Commerce.DtoModels.SubCategorydto;
using E_Commerce.Enums;
using E_Commerce.ErrorHnadling;
using E_Commerce.Interfaces;
using E_Commerce.Models;
using E_Commerce.Services.AdminOpreationServices;
using E_Commerce.Services.Cache;
using E_Commerce.Services.EmailServices;
using E_Commerce.UOW;
using Hangfire;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.Linq.Expressions;
using System.Threading.Tasks;

namespace E_Commerce.Services.CategoryServcies
{
	public class CategoryServices : ICategoryServices
	{
		private readonly ILogger<CategoryServices> _logger;
		private readonly IMapper _mapping;
		private readonly IUnitOfWork _unitOfWork;
		private readonly IAdminOpreationServices _adminopreationservices;
		private readonly IBackgroundJobClient _backgroundJobClient;
		private readonly ICacheManager _cacheManager;
		private readonly IImagesServices _imagesServices;
		private const string CACHE_TAG_CATEGORY = "category";
		public const string CACHE_TAG_CATEGORY_WITH_DATA = "categorywithdata";
		private  readonly string[] CACHE_TAGS_CATEGORY = new string[] { CACHE_TAG_CATEGORY, CACHE_TAG_CATEGORY_WITH_DATA };

		public CategoryServices(

			IBackgroundJobClient backgroundJobClient,
			
			 IImagesServices imagesServices,
			IAdminOpreationServices adminopreationservices,
			ICacheManager cacheManager,
			IMapper mapping,
			IUnitOfWork unitOfWork,
			ILogger<CategoryServices> logger
		)
		{ 
			 _backgroundJobClient = backgroundJobClient ?? throw new ArgumentNullException(nameof(backgroundJobClient));
			
			_imagesServices = imagesServices;
			_adminopreationservices = adminopreationservices;
			_cacheManager = cacheManager;
			_mapping = mapping;
			_logger = logger;
			_unitOfWork = unitOfWork;
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



		private static readonly Expression<Func<E_Commerce.Models.Category, CategoryDto>> CategorySelector = c => new CategoryDto
		{
			Id = c.Id,
			Name = c.Name,
			Description = c.Description,
			IsActive = c.IsActive,
			CreatedAt = c.CreatedAt,
			DeletedAt = c.DeletedAt,
			ModifiedAt = c.ModifiedAt,
			DisplayOrder = c.DisplayOrder,
			Images = c.Images.Select(i => new ImageDto
			{
				Id = i.Id,
				Url = i.Url,
				IsMain = i.IsMain
			}).ToList()
		};
		private static readonly Expression<Func<E_Commerce.Models.Category, CategorywithdataDto>> CategorySelectorWithData = c => new CategorywithdataDto
		{
			Id = c.Id,
			Name = c.Name,
			Description = c.Description,
			IsActive = c.IsActive,
			CreatedAt = c.CreatedAt,
			DeletedAt = c.DeletedAt,
			ModifiedAt = c.ModifiedAt,
			DisplayOrder = c.DisplayOrder,
			SubCategories = c.SubCategories.Where(sc=>sc.IsActive&&sc.DeletedAt==null).Select(sc => new SubCategoryDto
			{
				Id = sc.Id,
				Name = sc.Name,
				Description = sc.Description,
				IsActive = sc.IsActive,
				CreatedAt = sc.CreatedAt,
				ModifiedAt = sc.ModifiedAt,
				DeletedAt = sc.DeletedAt,
				Images = sc.Images.Select(i => new ImageDto
				{
					Id = i.Id,
					Url = i.Url,
					IsMain = i.IsMain
				}).ToList()
			}).ToList(),
			Images = c.Images.Select(i => new ImageDto
			{
				Id = i.Id,
				Url = i.Url,
				IsMain = i.IsMain
			}).ToList()
		};
		private async Task<CategorywithdataDto?> privateGetCategoryByIdAsync(int id, bool? isActive = null, bool? isDeleted = null)
		{
			_logger.LogInformation($"Executing {nameof(GetCategoryByIdAsync)} for id: {id}");

			var query =_unitOfWork.Category.GetAll();

			query = query.Where(c => c.Id == id);

			query = BasicFilter(query, isActive, isDeleted);

			var category = await query.Select(CategorySelectorWithData)
				.FirstOrDefaultAsync();

			if (category == null)
			{
				_logger.LogWarning($"Category with id: {id} doesn't exist");
				return null;
			}

			_logger.LogInformation($"Category with id: {id} exists");
			return category;
		}
		private async Task<Category?> GetCategoryByIdWithImagesAsync(int id, bool? isActive = null, bool? isDeleted = null)
		{
			_logger.LogInformation($"Executing {nameof(GetCategoryByIdAsync)} for id: {id}");

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
		private async Task<Category?> GetCategoryByIdWithSubCategoryAsync(int id, bool? isActive = null, bool? isDeleted = null)
		{
			_logger.LogInformation($"Executing {nameof(GetCategoryByIdAsync)} for id: {id}");

			var query = _unitOfWork.Category.GetAll();

			query = query.Where(c => c.Id == id);

			query = BasicFilter(query, isActive, isDeleted);

			var category = await query
				.Include(c => c.SubCategories.Where(sc => sc.DeletedAt == null && sc.IsActive == true))
				.FirstOrDefaultAsync();

			if (category == null)
			{
				_logger.LogWarning($"Category with id: {id} doesn't exist");
				return null;
			}

			_logger.LogInformation($"Category with id: {id} exists");
			return category;
		}

		private   void RemoveCategoryCache()
		{
			
			_backgroundJobClient.Enqueue( ()=>  _cacheManager.RemoveByTagsAsync(CACHE_TAGS_CATEGORY));
		}
		private CategoryDto maptocategorydto(E_Commerce.Models. Category c)=>new CategoryDto 
		{
			Id = c.Id,
			Name = c.Name,
			Description = c.Description,
			IsActive = c.IsActive,
			CreatedAt = c.CreatedAt,
			DeletedAt = c.DeletedAt,
			ModifiedAt = c.ModifiedAt,
			DisplayOrder = c.DisplayOrder,
			Images = c.Images.Select(i => new ImageDto
			{
				Id = i.Id,
				Url = i.Url,
				IsMain = i.IsMain
			}).ToList()
		};

		private async Task<Result< List<CategoryDto>> >categoryDtos(string? word, bool? isActive = null, bool? isDeleted = null, int page = 1, int pageSize = 10)
		{
			var cacheKey = $"category_all_{word}_{isActive}_{isDeleted}_{page}_{pageSize}";

			var query = _unitOfWork.Category.GetAll();
			query = BasicFilter(query, isActive, isDeleted);
			if (!string.IsNullOrWhiteSpace(word))
			{
				query = query.Where(c => EF.Functions.Like(c.Name, $"%{word}%") || EF.Functions.Like(c.Description, $"%{word}%"));
			}
			var result=await	query.Skip((page - 1) * pageSize)
			.Take(pageSize).Select(CategorySelector).ToListAsync();
			BackgroundJob.Enqueue(() => _cacheManager.SetAsync(cacheKey, result, null, new[] { CACHE_TAG_CATEGORY }));
			return Result<List<CategoryDto>>.Ok(result, "Categories fetched", 200);
		}

	


	
		public async Task<Result<bool>> IsExsistAsync(int id)
		{
			_logger.LogInformation($"Execute:{nameof(IsExsistAsync)} in Category Services");
			var exists = await _unitOfWork.Category.IsExsistAsync(id);
			if (exists)
				return Result<bool>.Ok(true, "category Exsist", 200);
			else
				return Result<bool>.Fail($"No Categoty with this id:{id}", 404);
		}

		public async Task<Result<CategorywithdataDto>> GetCategoryByIdAsync(int id, bool? isActive = null, bool? IsDeleted = false)
		{
			_logger.LogInformation($"Execute: {nameof(GetCategoryByIdAsync)} in services for id: {id}, isActive: {isActive}, includeDeleted: {IsDeleted}");

			var cacheKey = $"{CACHE_TAG_CATEGORY}id:{id}_active:{isActive}_deleted:{IsDeleted}";
			var cachedCategory = await _cacheManager.GetAsync<CategorywithdataDto>(cacheKey);
			if (cachedCategory != null)
			{
				_logger.LogInformation($"Cache hit for category {id} with filters");
				return Result<CategorywithdataDto>.Ok(cachedCategory, "Category found in cache", 200);
			}

			var categoryDto = await privateGetCategoryByIdAsync(id, isActive, IsDeleted);
			
				
			if (categoryDto == null)
			{
				_logger.LogWarning($"Category with id: {id} not found");
				return Result<CategorywithdataDto>.Fail($"Category with id: {id} not found", 404);
			}


		
			BackgroundJob.Enqueue(() => _cacheManager.SetAsync(cacheKey, categoryDto, null, new[] { CACHE_TAG_CATEGORY_WITH_DATA }));
			return Result<CategorywithdataDto>.Ok(categoryDto, "Category found", 200);
		}

		private void NotifyAdminOfError(string message, string? stackTrace = null)
		{
			_backgroundJobClient.Enqueue<IErrorNotificationService>(_ => _.SendErrorNotificationAsync(message, stackTrace));
		}

		private CategorywithdataDto MapToCategoryWithDataDto(E_Commerce.Models. Category c)
		{
			return new CategorywithdataDto
			{
				Id = c.Id,
				Name = c.Name,
				Description = c.Description,
				DisplayOrder = c.DisplayOrder,
				IsActive = c.IsActive,
				CreatedAt = c.CreatedAt,
				ModifiedAt = c.ModifiedAt,
				DeletedAt = c.DeletedAt,
				Images = c.Images?.Select(i => new ImageDto
				{
					Id = i.Id,
					Url = i.Url,
					IsMain = i.IsMain
				}).ToList() ?? new List<ImageDto>(),
				SubCategories = c.SubCategories?.Select(sc => new SubCategoryDto
				{
					Id = sc.Id,
					Name = sc.Name,
					Description = sc.Description,
					IsActive = sc.IsActive,
					CreatedAt = sc.CreatedAt,
					ModifiedAt = sc.ModifiedAt,
					DeletedAt = sc.DeletedAt,
					Images = sc.Images?.Select(i => new ImageDto
					{
						Id = i.Id,
						Url = i.Url,
						IsMain = i.IsMain
					}).ToList() ?? new List<ImageDto>()
				}).ToList() ?? new List<SubCategoryDto>()
			};
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
					 Description= model.Description,
					 Name= model.Name,
					 IsActive=false,
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
				 RemoveCategoryCache();
				await _unitOfWork.CommitAsync();
				await transaction.CommitAsync();
				var categoryaftercreate= await _unitOfWork.Category.GetByIdAsync(category.Id);
				if (categoryaftercreate == null)
				{
					_logger.LogError("Failed to retrieve created category");
					NotifyAdminOfError($"Failed to retrieve created category with ID {category.Id} after creation");
					return Result<CategoryDto>.Fail("Category created but failed to retrieve details", 500);
				}
				var categoryDto = maptocategorydto(categoryaftercreate);
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

		public async Task<Result<List<ImageDto>>> AddImagesToCategoryAsync(int categoryId, List<IFormFile> images, string userId)
		{
			_logger.LogInformation($"Executing {nameof(AddImagesToCategoryAsync)} for categoryId: {categoryId}");

			if (categoryId <= 0)
				return Result<List<ImageDto>>.Fail("Invalid category ID", 400);
			
			if (string.IsNullOrWhiteSpace(userId))
				return Result<List<ImageDto>>.Fail("User ID cannot be empty", 400);

			if (images == null || !images.Any())
				return Result<List<ImageDto>>.Fail("At least one image is required.", 400);

			using var transaction = await _unitOfWork.BeginTransactionAsync();
			try
			{
				var categoryExists = await _unitOfWork.Category.IsExsistAsync(categoryId);
				if (!categoryExists)
				{
					await transaction.RollbackAsync();
					return Result<List<ImageDto>>.Fail($"Category with id {categoryId} not found", 404);
				}

				var imageResult = await _imagesServices.SaveCategoryImagesAsync(images, categoryId, userId);

				if (!imageResult.Success || imageResult.Data == null || !imageResult.Data.Any())
				{
					await transaction.RollbackAsync();
					return Result<List<ImageDto>>.Fail($"Failed to save images: {imageResult.Message}", 400);
				}

		
				var adminLog = await  _adminopreationservices.AddAdminOpreationAsync(
				
					$"Added {imageResult.Data.Count} images to category",
					Opreations.AddOpreation,
					userId,
					categoryId
				);
				if (!adminLog.Success)
				{
					await transaction.RollbackAsync();
					_logger.LogWarning($"Failed to log admin operation: {adminLog.Message}");
					return Result<List<ImageDto>>.Fail("An error occurred while deleting category", 500);
				}

				await _unitOfWork.CommitAsync();
				await transaction.CommitAsync();

				var mapped = _mapping.Map<List<ImageDto>>(imageResult.Data);
				RemoveCategoryCache();

				return Result<List<ImageDto>>.Ok(mapped, $"Added {imageResult.Data.Count} images to category", 200, warnings: imageResult.Warnings);
			}
			catch (Exception ex)
			{
				await transaction.RollbackAsync();
				_logger.LogError($"Exception in {nameof(AddImagesToCategoryAsync)}: {ex.Message}");
				NotifyAdminOfError($"Exception in AddImagesToCategoryAsync: {ex.Message}", ex.StackTrace);
				return Result<List<ImageDto>>.Fail("An error occurred while adding images", 500);
			}
		}


		public async Task<Result<bool>> DeleteAsync(int categoryId, string userid)
		{
			_logger.LogInformation($"Executing {nameof(DeleteAsync)} for categoryId: {categoryId}");

			if (categoryId <= 0)
				return Result<bool>.Fail("Invalid category ID", 400);

			if (string.IsNullOrWhiteSpace(userid))
				return Result<bool>.Fail("User ID cannot be empty", 400);

			using var transaction = await _unitOfWork.BeginTransactionAsync();

			try
			{
				var categoryInfo = await _unitOfWork.Category.GetAll()
					.Where(c => c.Id == categoryId)
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
					return Result<bool>.Fail($"Category with id {categoryId} not found or already deleted", 404);
				}

				if (categoryInfo.HasSubCategories)
				{
					await transaction.RollbackAsync();
					_logger.LogWarning($"Category {categoryId} contains subcategories");
					return Result<bool>.Fail("Can't delete category because it has subcategories", 400);
				}

				var deleteResult = await _unitOfWork.Category.SoftDeleteAsync(categoryId);
				if (!deleteResult)
				{
					await transaction.RollbackAsync();
					return Result<bool>.Fail($"Failed to delete category", 500);
				}

				var adminLog = await _adminopreationservices.AddAdminOpreationAsync(
					$"Deleted Category {categoryId}",
					Opreations.DeleteOpreation,
					userid,
					categoryId
				);

				if (!adminLog.Success)
				{
					await transaction.RollbackAsync();
					_logger.LogWarning($"Failed to log admin operation: {adminLog.Message}");
					return Result<bool>.Fail("An error occurred while deleting category", 500);
				}

				await _unitOfWork.CommitAsync();
				await transaction.CommitAsync();

				RemoveCategoryCache();

				return Result<bool>.Ok(true, $"Category with ID {categoryId} deleted successfully", 200);
			}
			catch (Exception ex)
			{
				await transaction.RollbackAsync();
				_logger.LogError($"Exception in DeleteAsync: {ex.Message}");
				NotifyAdminOfError($"Exception in DeleteAsync: {ex.Message}", ex.StackTrace);
				return Result<bool>.Fail("An error occurred while deleting category", 500);
			}
		}

		public async Task<Result<List<CategoryDto>>> FilterAsync(
	string? search,
	bool? isActive=null,
	bool ? isDeleted = null,
	int page=1,
	int pageSize=10)
		{
			_logger.LogInformation($"Executing {nameof(FilterAsync)} with filters");
			var cacheKey = $"category_all_{search}_{isActive}_{isDeleted}_{page}_{pageSize}";
			var cached = await _cacheManager.GetAsync<List<CategoryDto>>(cacheKey);
			if (cached != null)
			{
				_logger.LogInformation($"Cache hit for FilterAsync with key: {cacheKey}");
				return Result<List<CategoryDto>>.Ok(cached, "Categories fetched from cache", 200);
			}

			return await categoryDtos(search, isActive, isDeleted, page, pageSize);


		}


		public async Task<Result<CategoryDto>> ReturnRemovedCategoryAsync(int id, string userid)
		{
			_logger.LogInformation($"Executing {nameof(ReturnRemovedCategoryAsync)} for id: {id}");
			
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
				 RemoveCategoryCache();
				
				// Get the restored category with images for proper mapping
				var restoredCategory = await GetCategoryByIdWithImagesAsync(id);
				if (restoredCategory == null)
				{
					_logger.LogError("Failed to retrieve restored category");
					return Result<CategoryDto>.Fail("Category restored but failed to retrieve details", 500);
				}
				
				var categorydto = maptocategorydto(restoredCategory);
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




		public async Task<Result<bool>> ActivateCategoryAsync(int categoryId, string userId)
		{
			using var transaction = await _unitOfWork.BeginTransactionAsync();
			_logger.LogInformation($"[ActivateCategory] Start activation for CategoryId: {categoryId}");

			try
			{
				var categoryInfo = await _unitOfWork.Category.GetAll()
					.Where(c => c.Id == categoryId && c.DeletedAt == null)
					.Select(c => new
					{
						IsActive = c.IsActive,
						HasActiveSubCategories = c.SubCategories.Any(sc => sc.IsActive && sc.DeletedAt == null),
						HasImages = c.Images.Any(i => i.DeletedAt == null)
					})
					.FirstOrDefaultAsync();

				if (categoryInfo == null)
				{
					_logger.LogWarning($"[ActivateCategory] Category {categoryId} not found.");
					return Result<bool>.Fail($"Category with id {categoryId} not found", 404);
				}

				if (categoryInfo.IsActive)
				{
					_logger.LogWarning($"[ActivateCategory] Category {categoryId} is already active.");
					return Result<bool>.Fail($"Category with id {categoryId} is already active", 400);
				}

				if (!categoryInfo.HasImages)
				{
					_logger.LogWarning($"[ActivateCategory] Category {categoryId} has no active images.");
					return Result<bool>.Fail("Cannot activate category without at least one image", 400);
				}

				if (!categoryInfo.HasActiveSubCategories)
				{
					_logger.LogWarning($"[ActivateCategory] Category {categoryId} has no active subcategories.");
					return Result<bool>.Fail("Cannot activate category without at least one active subcategory", 400);
				}

				if (!await _unitOfWork.Category.ActiveCategoryAsync(categoryId))
				{
					_logger.LogError($"[ActivateCategory] Failed to activate category {categoryId}");
					return Result<bool>.Fail("Failed to activate category", 500);
				}

				var adminOpResult = await _adminopreationservices.AddAdminOpreationAsync(
					"Activate Category",
					Opreations.UpdateOpreation,
					userId,
					categoryId
				);

				if (!adminOpResult.Success)
				{
					await transaction.RollbackAsync();
					_logger.LogError($"[ActivateCategory] Failed to log admin operation: {adminOpResult.Message}");
					return Result<bool>.Fail("An error occurred while logging admin operation", 500);
				}

				await _unitOfWork.CommitAsync();
				await transaction.CommitAsync();
				RemoveCategoryCache();

				_logger.LogInformation($"[ActivateCategory] Category {categoryId} activated successfully.");
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

		public async Task<Result<bool>> DeactivateCategoryAsync(int categoryId, string userId)
		{
			using var transaction = await _unitOfWork.BeginTransactionAsync();
			_logger.LogInformation($"[DeactivateCategory] Starting deactivation process for Category ID: {categoryId}");

			try
			{
				var categoryInfo = await _unitOfWork.Category.GetAll()
					.Where(c => c.Id == categoryId)
					.Select(c => new
					{
						IsActive = c.IsActive,
						DeletedAt = c.DeletedAt,
						HasActiveSubCategories = c.SubCategories.Any(sc => sc.IsActive && sc.DeletedAt == null)
					})
					.FirstOrDefaultAsync();

				if (categoryInfo == null)
				{
					_logger.LogWarning($"[DeactivateCategory] Category {categoryId} not found.");
					return Result<bool>.Fail($"Category with ID {categoryId} not found.", 404);
				}

				if (!categoryInfo.IsActive || categoryInfo.DeletedAt != null)
				{
					_logger.LogWarning($"[DeactivateCategory] Category {categoryId} is already deactivated.");
					return Result<bool>.Fail($"Category with ID {categoryId} is already deactivated.", 400);
				}

				if (categoryInfo.HasActiveSubCategories)
				{
					_logger.LogWarning($"[DeactivateCategory] Category {categoryId} still has active subcategories.");
					return Result<bool>.Fail("Cannot deactivate category while it still has active subcategories.", 400);
				}

				if (!await _unitOfWork.Category.DeactiveCategoryAsync(categoryId))
				{
					_logger.LogError($"[DeactivateCategory] Failed to deactivate category {categoryId}.");
					return Result<bool>.Fail("Failed to deactivate the category.", 500);
				}

				var adminOpResult = await _adminopreationservices.AddAdminOpreationAsync(
					"Deactivate Category",
					Opreations.UpdateOpreation,
					userId,
					categoryId
				);

				if (!adminOpResult.Success)
				{
					await transaction.RollbackAsync();
					_logger.LogWarning($"[DeactivateCategory] Failed to log admin operation: {adminOpResult.Message}");
					return Result<bool>.Fail("An error occurred while logging the admin operation.", 500);
				}

				await _unitOfWork.CommitAsync();
				await transaction.CommitAsync();
				RemoveCategoryCache();

				_logger.LogInformation($"[DeactivateCategory] Category {categoryId} deactivated successfully.");
				return Result<bool>.Ok(true, "Category deactivated successfully.", 200);
			}
			catch (Exception ex)
			{
				await transaction.RollbackAsync();
				_logger.LogError(ex, $"[DeactivateCategory] Unexpected error occurred for category {categoryId}.");
				NotifyAdminOfError($"Exception in DeactivateCategoryAsync: {ex.Message}", ex.StackTrace);
				return Result<bool>.Fail("An unexpected error occurred while deactivating the category.", 500);
			}
		}


		public async Task<Result<CategoryDto>> UpdateAsync(int categoryId, UpdateCategoryDto category, string userid)
		{
			_logger.LogInformation($"Executing {nameof(UpdateAsync)} for categoryId: {categoryId}");

			var existingCategory = await _unitOfWork.Category.GetByIdAsync(categoryId);
			if (existingCategory == null)
			{
				return Result<CategoryDto>.Fail($"Category with id {categoryId} not found", 404);
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

				 RemoveCategoryCache();

				var dto = MapToCategoryWithDataDto(existingCategory);
				return Result<CategoryDto>.Ok(dto, "Updated", 200, warnings: warings);
			}
				
			catch (Exception ex)
			{
				await transaction.RollbackAsync();
				_logger.LogError($"Exception in UpdateAsync: {ex.Message}");
				return Result<CategoryDto>.Fail("An error occurred during update", 500);
			}
		}




		public async Task<Result<ImageDto>> AddMainImageToCategoryAsync(int categoryId, IFormFile mainImage, string userId)
		{
			_logger.LogInformation($"Executing {nameof(AddMainImageToCategoryAsync)} for categoryId: {categoryId}");
			if (mainImage == null || mainImage.Length == 0)
			{
				return Result<ImageDto>.Fail("Main image is required.", 400);
			}
			
			using var transaction = await _unitOfWork.BeginTransactionAsync();
			try
			{
				var category = await _unitOfWork.Category.IsExsistAsync(categoryId);
				if (!category)
				{
					await transaction.RollbackAsync();
					return Result<ImageDto>.Fail($"Category with id {categoryId} not found", 404);
				}
				
				var mainImageResult = await _imagesServices.SaveMainCategoryImageAsync(mainImage,categoryId, userId);
				if (!mainImageResult.Success || mainImageResult.Data == null)
				{
					await transaction.RollbackAsync();
					return Result<ImageDto>.Fail($"Failed to save main image: {mainImageResult.Message}", 500);
				}
				
				await _unitOfWork.CommitAsync();
				await transaction.CommitAsync();
				
				RemoveCategoryCache();

				var mapped = _mapping.Map<ImageDto>(mainImageResult.Data);
				return Result<ImageDto>.Ok(mapped, "Main image added to category", 200);
			}
			catch (Exception ex)
			{
				await transaction.RollbackAsync();
				_logger.LogError($"Exception in AddMainImageToCategoryAsync: {ex.Message}");
				NotifyAdminOfError($"Exception in AddMainImageToCategoryAsync: {ex.Message}", ex.StackTrace);
				return Result<ImageDto>.Fail("An error occurred while adding main image", 500);
			}
		}

		public async Task<Result<bool>> RemoveImageFromCategoryAsync(int categoryId, int imageId, string userId)
		{
			_logger.LogInformation($"Removing image {imageId} from category: {categoryId}");

			using var transaction = await _unitOfWork.BeginTransactionAsync();

			try
			{
				var categoryData = await _unitOfWork.Category.GetAll()
					.Where(c => c.Id == categoryId)
					.Select(c => new
					{
						Exists = true,
						IsActive = c.IsActive,
						HasImage = c.Images.Any(i => i.Id == imageId),
						ImagesCount = c.Images.Count
					})
					.FirstOrDefaultAsync();

				if (categoryData == null)
				{
					await transaction.RollbackAsync();
					return Result<bool>.Fail($"Category with id {categoryId} not found", 404);
				}

				if (!categoryData.HasImage)
				{
					await transaction.RollbackAsync();
					return Result<bool>.Fail("Image not found", 404);
				}

				await _unitOfWork.Image.SoftDeleteAsync(imageId);

			
				int remainingImages = categoryData.ImagesCount - 1;

				if (remainingImages == 0 && categoryData.IsActive)
				{
					var result= await DeactivateCategoryAsync(categoryId,userId);
					if(!result.Success)
					{
						await transaction.RollbackAsync();
						return  Result<bool>.Fail("Can't Delete All Imags It Become Deactive", 400);
					}
					_logger.LogInformation($"Category {categoryId} deactivated because it has no images left.");
				}

				var adminLog = await _adminopreationservices.AddAdminOpreationAsync(
					$"Remove Image {imageId} from Category {categoryId}",
					Opreations.UpdateOpreation,
					userId,
					categoryId
				);

				if (!adminLog.Success)
				{
					_logger.LogWarning($"Failed to log admin operation: {adminLog.Message}");
					await transaction.RollbackAsync();
					return Result<bool>.Fail($"Failed to log admin operation: {adminLog.Message}", 500);
				}

				await _unitOfWork.CommitAsync();
				await transaction.CommitAsync();

				RemoveCategoryCache();

				return Result<bool>.Ok(true, "Image removed successfully", 200);
			}
			catch (Exception ex)
			{
				await transaction.RollbackAsync();
				_logger.LogError(ex, $"Unexpected error in RemoveImageFromCategoryAsync for category {categoryId}");
				NotifyAdminOfError(ex.Message, ex.StackTrace);
				return Result<bool>.Fail("Unexpected error occurred while removing image", 500);
			}
		}


	}
}
