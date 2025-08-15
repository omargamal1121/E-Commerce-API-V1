using AutoMapper;
using E_Commerce.DtoModels.CategoryDtos;

using E_Commerce.DtoModels.ImagesDtos;
using E_Commerce.DtoModels.ProductDtos;
using E_Commerce.DtoModels.SubCategorydto;
using E_Commerce.Enums;

using E_Commerce.Interfaces;
using E_Commerce.Models;
using E_Commerce.Services.AdminOpreationServices;
using E_Commerce.Services.Cache;
using E_Commerce.Services.EmailServices;
using E_Commerce.UOW;
using Hangfire;

using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;


namespace E_Commerce.Services.SubCategoryServices
{
    public class SubCategoryServices : ISubCategoryServices
    {
        private readonly ILogger<SubCategoryServices> _logger;
        private readonly IMapper _mapping;
        private readonly IUnitOfWork _unitOfWork;
        private readonly IAdminOpreationServices _adminopreationservices;

        private readonly IBackgroundJobClient _backgroundJobClient;
        private readonly ICategoryServices _categoryServices;

		private readonly ICacheManager _cacheManager;
        private readonly IImagesServices _imagesServices;
        private const string CACHE_TAG_SUBCATEGORY = "subcategory";
		public const string CACHE_TAG_CATEGORY_WITH_DATA = "categorywithdata";
		private const string SUBCATEGORY_DATA_TAG = "subcategorydata";
        private static readonly string[] SUBCATEGORY_CACHE_TAGS = new[] { SUBCATEGORY_DATA_TAG, CACHE_TAG_CATEGORY_WITH_DATA, CACHE_TAG_SUBCATEGORY };

        public SubCategoryServices(

            ICategoryServices categoryServices,
			IBackgroundJobClient backgroundJobClient,
			IImagesServices imagesServices,
            IAdminOpreationServices adminopreationservices,
            ICacheManager cacheManager,
            IMapper mapping,
            IUnitOfWork unitOfWork,
            ILogger<SubCategoryServices> logger
        )
        {
            _categoryServices = categoryServices;
			_backgroundJobClient = backgroundJobClient;

			_imagesServices = imagesServices;
            _adminopreationservices = adminopreationservices;
            _cacheManager = cacheManager;
            _mapping = mapping;
            _logger = logger;
            _unitOfWork = unitOfWork;
        }

        private void NotifyAdminOfError(string message, string? stackTrace = null)
        {
            _backgroundJobClient.Enqueue<IErrorNotificationService>( (_)=>_ .SendErrorNotificationAsync(message, stackTrace));
        }

      


        private void RemoveSubCategoryCaches()
        {
            _backgroundJobClient.Enqueue(() => _cacheManager.RemoveByTagsAsync(SUBCATEGORY_CACHE_TAGS));
        }

     

        public async Task<Result<bool>> IsExsistAsync(int id)
        {
                _logger.LogInformation($"Execute:{nameof(IsExsistAsync)} in SubCategory Services");
            var exists = await _unitOfWork.SubCategory.IsExsistAsync(id);
               if(exists)
                {
                    _logger.LogInformation($"SubCategory with id: {id} exists");
                    return Result<bool>.Ok(true, "SubCategory exists", 200);
		    	}
            return Result<bool>.Fail("subcategory Exsist", 401);
             
        }



		public static Expression<Func<SubCategory, SubCategoryDtoWithData>> MapToDtoWithDataExpression =>
	subCategory => new SubCategoryDtoWithData
	{
		Id = subCategory.Id,
		Name = subCategory.Name,
		IsActive = subCategory.IsActive,
		CreatedAt = subCategory.CreatedAt,
		ModifiedAt = subCategory.ModifiedAt,
		DeletedAt = subCategory.DeletedAt,
		Description = subCategory.Description,

		Images = subCategory.Images
			.Where(img => img.DeletedAt == null)
			.Select(img => new ImageDto
			{
				Id = img.Id,
				IsMain = img.IsMain,
				Url = img.Url
			}).ToList(),

		Products = subCategory.Products.Where(p=>p.IsActive&&p.DeletedAt==null).Select(p => new ProductDto
		{

			Id = p.Id,
			Name = p.Name,
			Description = p.Description,
			AvailableQuantity = p.Quantity,
			Gender = p.Gender,
			SubCategoryId = p.SubCategoryId,
			Price = p.Price,
			CreatedAt = p.CreatedAt,
			ModifiedAt = p.ModifiedAt,
			DeletedAt = p.DeletedAt,
			FinalPrice = (p.Discount != null && p.Discount.IsActive && (p.Discount.DeletedAt == null) && (p.Discount.EndDate > DateTime.UtcNow)) ? Math.Round(p.Price - (((p.Discount.DiscountPercent) / 100) * p.Price)) : p.Price,
			fitType = p.fitType,
			images = p.Images.Where(i => i.DeletedAt == null).Select(i => new ImageDto { Id = i.Id, Url = i.Url }).ToList(),
			EndAt = (p.Discount != null && p.Discount.IsActive && p.Discount.EndDate > DateTime.UtcNow) && p.Discount.IsActive ? p.Discount.EndDate : null,
			DiscountName = (p.Discount != null && p.Discount.IsActive && p.Discount.EndDate > DateTime.UtcNow) ? p.Discount.Name : null,
			DiscountPrecentage = (p.Discount != null && p.Discount.IsActive && p.Discount.EndDate > DateTime.UtcNow) ? p.Discount.DiscountPercent : 0,
			IsActive = p.IsActive,
		}).ToList()
	};

		public static Expression<Func<SubCategory, SubCategoryDto>> MapToDtoExpression =>
	subCategory => new SubCategoryDto
	{
		Id = subCategory.Id,
		Name = subCategory.Name,
		IsActive = subCategory.IsActive,
		CreatedAt = subCategory.CreatedAt,
		ModifiedAt = subCategory.ModifiedAt,
		DeletedAt = subCategory.DeletedAt,
		Description = subCategory.Description,
		Images = subCategory.Images
		.Where(img => img.DeletedAt == null)
		.Select(img => new ImageDto
		{
			Id = img.Id,
			Url = img.Url
		}).ToList(),

	};
		private IQueryable<SubCategory> BasicFilter(IQueryable<SubCategory> query, bool? isActive, bool? DeletedOnly)
		{
			if (isActive.HasValue)
			{
				if (isActive.Value)
					query = query.Where(p => p.IsActive);
				else
					query = query.Where(p => !p.IsActive);
			}
			if (DeletedOnly.HasValue)
			{
				if (DeletedOnly.Value)
					query = query.Where(p => p.DeletedAt != null);
				else
					query = query.Where(p => p.DeletedAt == null);
			}
			return query;
		}

		private async Task<List<SubCategoryDto>> privateFilterSubCategoryAsync(string? search, bool? isActive = null, bool? isDeleted = null, int page = 1, int pagesize = 10)
		{
            var query = _unitOfWork.SubCategory.GetAll();
			if (!string.IsNullOrWhiteSpace(search))
				query = query.Where(sc => EF.Functions.Like(sc.Name, $"%{search}%") ||
			EF.Functions.Like(sc.Description, $"%{search}%"));
			query = BasicFilter(query, isActive, isDeleted);

            

			var subCategories = await query
				   .OrderBy(sc => sc.Id)
				   .Skip((page - 1) * pagesize)
				   .Take(pagesize)
				   .Select(MapToDtoExpression)
				   .ToListAsync();
			return subCategories;
		}



		public SubCategoryDto MaptoSubCategoryDto(SubCategory subCategory)=> new SubCategoryDto
        {
            Id = subCategory.Id,
            Name = subCategory.Name,
            IsActive = subCategory.IsActive,
            CreatedAt = subCategory.CreatedAt,
            ModifiedAt = subCategory.ModifiedAt,
            DeletedAt = subCategory.DeletedAt,
            Description = subCategory.Description,
            Images = subCategory.Images?.Select(img => new ImageDto
            {
                Id = img.Id,
                Url = img.Url
            }).ToList()
        };
		public SubCategoryDtoWithData MapToSubCategoryDtoWithData(SubCategory subCategory)
		{
			return new SubCategoryDtoWithData
			{
				Id = subCategory.Id,
				Name = subCategory.Name,
				IsActive = subCategory.IsActive,
				Images = subCategory.Images?.Select(img => new ImageDto
				{
					Id = img.Id,
					Url = img.Url
				}).ToList(),
                Description= subCategory.Description,
                DeletedAt= subCategory.DeletedAt,
                CreatedAt = subCategory.CreatedAt,
                ModifiedAt = subCategory.ModifiedAt,
				Products = subCategory.Products?.Select(p => new ProductDto
				{
					Id = p.Id,
					Name = p.Name,
					IsActive = p.IsActive,
                    AvailableQuantity = p.Quantity,
                    Price = p.Price,    
                    Description = p.Description,
                    SubCategoryId = p.SubCategoryId,
                    CreatedAt = p.CreatedAt,
                    DiscountPrecentage= (p.Discount != null && p.Discount.IsActive && (p.Discount.DeletedAt == null) && (p.Discount.EndDate > DateTime.UtcNow)) ?p.Discount.DiscountPercent:null,
					FinalPrice = (p.Discount != null && p.Discount.IsActive && (p.Discount.DeletedAt == null) && (p.Discount.EndDate > DateTime.UtcNow)) ? Math.Round(p.Price - ((p.Discount.DiscountPercent / 100) * p.Price)) : p.Price,

					DiscountName = (p.Discount != null && p.Discount.IsActive && (p.Discount.DeletedAt == null) && (  p.Discount.EndDate > DateTime.UtcNow)) ?p.Discount.Name : null,
					EndAt = (p.Discount != null && p.Discount.IsActive && (p.Discount.DeletedAt == null) && (   p.Discount.EndDate > DateTime.UtcNow)) ?p.Discount.EndDate : null,
					fitType = p.fitType,
                    Gender=p.Gender,
   
					ModifiedAt = p.ModifiedAt,
                DeletedAt = p.DeletedAt,
                

					images = p.Images?.Select(img => new ImageDto
					{
						Id = img.Id,
                        IsMain= img.IsMain,
						Url = img.Url
					}).ToList()
				}).ToList()
			};
		}
		private async Task<SubCategoryDtoWithData?> privateGetSubCategoryByIdAsync(int id, bool? isActive = null, bool? isDeleted = null)
		{
			_logger.LogInformation($"Executing {nameof(privateGetSubCategoryByIdAsync)} for id: {id}");

			var query = _unitOfWork.SubCategory.GetAll();

			query = query.Where(c => c.Id == id);

			query = BasicFilter(query, isActive, isDeleted);

			var category = await query.Select(MapToDtoWithDataExpression)
				.FirstOrDefaultAsync();

			if (category == null)
			{
				_logger.LogWarning($"Category with id: {id} doesn't exist");
				return null;
			}

			_logger.LogInformation($"Category with id: {id} exists");
			return category;
		}

		public async Task<Result<SubCategoryDtoWithData>> GetSubCategoryByIdAsync(int id, bool? isActive = null, bool? isDeleted = null)
		{
			_logger.LogInformation($"Execute: {nameof(GetSubCategoryByIdAsync)} in services for id: {id}, isActive: {isActive}, isDeleted: {isDeleted}");

			var cacheKey = $"{SUBCATEGORY_DATA_TAG}id:{id}_active:{isActive}_deleted:{isDeleted}";
			var cached = await _cacheManager.GetAsync<SubCategoryDtoWithData>(cacheKey);
			if (cached != null)
			{
				_logger.LogInformation($"Cache hit for subcategory {id} with filters");
				return Result<SubCategoryDtoWithData>.Ok(cached, "SubCategory found in cache", 200);
			}
            
            
 
            var subCategory = await  privateGetSubCategoryByIdAsync(id, isActive, isDeleted);
			if (subCategory == null)
			{
				_logger.LogWarning($"SubCategory with id: {id} not found");
				return Result<SubCategoryDtoWithData>.Fail($"SubCategory with id: {id} not found", 404);
			}
           

			
			BackgroundJob.Enqueue(() => _cacheManager.SetAsync(cacheKey, subCategory, null, new string[]{ SUBCATEGORY_DATA_TAG }));
			return Result<SubCategoryDtoWithData>.Ok(subCategory, "SubCategory found", 200);
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
            
            var isexsist =   await _unitOfWork.SubCategory.IsExsistByNameAsync(subCategory.Name);
            if (isexsist)
            {
                return Result<SubCategoryDto>.Fail($"there's subcategory with this name:{subCategory.Name}", 409);
            }
            using var transaction = await _unitOfWork.BeginTransactionAsync();
            try
            {
                SubCategory subCategoryEntity = new SubCategory
                {
                    CategoryId= subCategory.CategoryId,
                    Name = subCategory.Name,
                     IsActive = false,
                     Description= subCategory.Description,
                     

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
                 RemoveSubCategoryCaches();
                await transaction.CommitAsync();
                  var subcategorydto= MaptoSubCategoryDto(creationResult);

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

		public async Task<Result<List<ImageDto>>> AddImagesToSubCategoryAsync(int subCategoryId, List<IFormFile> images, string userId)
		{
			_logger.LogInformation($"Executing {nameof(AddImagesToSubCategoryAsync)} for subCategoryId: {subCategoryId}");

			if (subCategoryId <= 0)
            {
                return Result<List<ImageDto>>.Fail("Invalid subCategoryId.", 400);
            }
            if (images == null || !images.Any())
            {
                return Result<List<ImageDto>>.Fail("At least one image is required.", 400);
            }

			var subCategory = await _unitOfWork.SubCategory.IsExsistAsync(subCategoryId);
			if (!subCategory)
			{
                
				return Result<List<ImageDto>>.Fail($"SubCategory with id {subCategoryId} not found", 404);
			}

			using var transaction = await _unitOfWork.BeginTransactionAsync();

			try
			{
				var imageResult = await _imagesServices.SaveSubCategoryImagesAsync(images,subCategoryId, userId);
				if (imageResult == null || imageResult.Data == null)
				{
					await transaction.RollbackAsync();
					return Result<List<ImageDto>>.Fail(imageResult?.Message ?? "Failed to save images", imageResult?.StatusCode ?? 500, imageResult?.Warnings);
				}

			

				var adminLog = await _adminopreationservices.AddAdminOpreationAsync(
					$"Added {imageResult.Data.Count} images to SubCategory {subCategoryId}",
					Opreations.UpdateOpreation,
					userId,
					subCategoryId
				);
				
				if (!adminLog.Success)
				{
					_logger.LogWarning($"Failed to log admin operation: {adminLog.Message}");
                    await transaction.RollbackAsync();
                    return Result<List<ImageDto>>.Fail("Failed to log admin operation", 500);
				}

				await _unitOfWork.CommitAsync();
				await transaction.CommitAsync();
				 RemoveSubCategoryCaches();

				var mapped = _mapping.Map<List<ImageDto>>(imageResult.Data);
				return Result<List<ImageDto>>.Ok(mapped, $"Added {imageResult.Data.Count} images to subcategory", 200, warnings: imageResult.Warnings);
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, $"Exception in {nameof(AddImagesToSubCategoryAsync)} for subCategoryId: {subCategoryId}");
				await transaction.RollbackAsync();
				 NotifyAdminOfError(ex.Message, ex.StackTrace);
				return Result<List<ImageDto>>.Fail("Unexpected error occurred while adding images", 500);
			}
		}

		public async Task<Result<bool>> DeleteAsync(int id, string userid)
        {
            _logger.LogInformation($"Executing {nameof(DeleteAsync)} for subCategoryId: {id}");
            
            using var transaction = await _unitOfWork.BeginTransactionAsync();
            try
            {

                var subcategoryinfo = await  _unitOfWork.SubCategory.GetAll().Where(c=>c.Id==id).Select(x => new
                {
                    isdelete= x.DeletedAt!=null,
                    ishasproducts= x.Products.Any(),
                    
                }).FirstOrDefaultAsync();
             
               
                if (subcategoryinfo == null)
                {
                    await transaction.RollbackAsync();
                    return Result<bool>.Fail($"SubCategory with id {id} not found", 404);
                }
                
                if(subcategoryinfo.isdelete)
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
                RemoveSubCategoryCaches();
                
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

        public async Task<Result<List<SubCategoryDto>>> FilterAsync(
            string? search,
            bool? isActive,
            bool?isDeleted,
            int page,
            int pageSize
            )
        {
            _logger.LogInformation($"Executing {nameof(FilterAsync)} with filters");
            
           
   
            string cacheKey = $"{CACHE_TAG_SUBCATEGORY}_filtered_{isActive}_{isDeleted}_p{page}_ps{pageSize}_{search}";
          
            
                var cachedData = await _cacheManager.GetAsync<List<SubCategoryDto>>(cacheKey);
                if (cachedData != null)
                    return Result<List<SubCategoryDto>>.Ok(cachedData, "Subcategories from cache", 200);
            
           var subCategories= await privateFilterSubCategoryAsync(search??string.Empty, isActive, isDeleted, page, pageSize);


			if (!subCategories.Any())
                return Result<List<SubCategoryDto>>.Fail("No subcategories found", 404);
           


				BackgroundJob.Enqueue(() => _cacheManager.SetAsync(cacheKey, subCategories, null,new string[]{ SUBCATEGORY_DATA_TAG }));
           
            return Result<List<SubCategoryDto>>.Ok(subCategories, "Filtered subcategories retrieved", 200);
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
                RemoveSubCategoryCaches();

                var dto = MapToSubCategoryDtoWithData(subCategory);
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

				bool hasChanges = false;

                if (subCategory == null)
                {
                    return Result<SubCategoryDto>.Fail("Update data is required", 400);
                }

                _logger.LogInformation($"Update data received - Name: '{subCategory.Name}', Description: '{subCategory.Description}', CategoryId: {subCategory.CategoryId}");
               
                if (!string.IsNullOrWhiteSpace(subCategory.Name?.Trim()) && subCategory.Name.Trim() != existingSubCategory.Name)
                {
                    var trimmedName = subCategory.Name.Trim();
                    
                    // Validate name format
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
                            hasChanges = true;
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
                        hasChanges = true;
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
                        hasChanges = true;
                        _logger.LogInformation("Description updated successfully");
                    }
                }

              
              
              
                if (hasChanges)
                {
                    existingSubCategory.ModifiedAt = DateTime.UtcNow;
                    _logger.LogInformation($"SubCategory {subCategoryId} has changes, updating ModifiedAt timestamp");
                    _logger.LogInformation($"Final entity state - Name: '{existingSubCategory.Name}', Description: '{existingSubCategory.Description}', CategoryId: {existingSubCategory.CategoryId}, IsActive: {existingSubCategory.IsActive}, ModifiedAt: {existingSubCategory.ModifiedAt}");
                    
                 
                    _logger.LogInformation($"Changes will be committed to database for SubCategory {subCategoryId}");
                }
                else
                {
                    _logger.LogInformation($"No changes detected for SubCategory {subCategoryId}");
                }

          
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
				RemoveSubCategoryCaches();
				_logger.LogInformation($"Successfully updated SubCategory {subCategoryId}");
                var dto =MapToSubCategoryDtoWithData(existingSubCategory);
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

		public async Task<Result<ImageDto>> AddMainImageToSubCategoryAsync(int subCategoryId, IFormFile mainImage, string userId)
		{
			_logger.LogInformation($"Executing {nameof(AddMainImageToSubCategoryAsync)} for subCategoryId: {subCategoryId}");
			if (subCategoryId <= 0)
            {
                return Result<ImageDto>.Fail("Invalid subCategoryId.", 400);
            }
            if (mainImage == null || mainImage.Length == 0)
            {
                return Result<ImageDto>.Fail("Main image is required.", 400);
            }
            var subCategory = await _unitOfWork.SubCategory.IsExsistAsync(subCategoryId);
			if (!subCategory)
			{
				return Result<ImageDto>.Fail($"SubCategory with id {subCategoryId} not found", 404);
			}
			
			using var transaction = await _unitOfWork.BeginTransactionAsync();
			try
			{
				
				var mainImageResult = await _imagesServices.SaveMainSubCategoryImageAsync(mainImage,subCategoryId, userId);
				if (mainImageResult == null || !mainImageResult.Success || mainImageResult.Data == null)
				{
					await transaction.RollbackAsync();
					return Result<ImageDto>.Fail(mainImageResult?.Message ?? "Failed to save main image", mainImageResult?.StatusCode ?? 500, mainImageResult?.Warnings);
				}
				
			
				
				var adminLog = await _adminopreationservices.AddAdminOpreationAsync(
					$"Added main image to SubCategory {subCategoryId}",
					Opreations.UpdateOpreation,
					userId,
					subCategoryId
				);
				
				if (!adminLog.Success)
				{
					_logger.LogWarning($"Failed to log admin operation: {adminLog.Message}");
                    await transaction.RollbackAsync();
                    return Result<ImageDto>.Fail("Failed to log admin operation", 500);
				}
				
				await _unitOfWork.CommitAsync();
				await transaction.CommitAsync();
				RemoveSubCategoryCaches();
				
				var mapped = _mapping.Map<ImageDto>(mainImageResult.Data);
				return Result<ImageDto>.Ok(mapped, "Main image added to subcategory", 200);
			}
			catch (Exception ex)
			{
				await transaction.RollbackAsync();
				_logger.LogError($"Exception in AddMainImageToSubCategoryAsync: {ex.Message}");
				NotifyAdminOfError($"Exception in AddMainImageToSubCategoryAsync: {ex.Message}", ex.StackTrace);
				return Result<ImageDto>.Fail("An error occurred while adding main image", 500);
			}
		}

		public async Task<Result<bool>> RemoveImageFromSubCategoryAsync(int subCategoryId, int imageId, string userId)
		{
			_logger.LogInformation($"Removing image {imageId} from subcategory: {subCategoryId}");

			using var transaction = await _unitOfWork.BeginTransactionAsync();
			try
			{
                var warnings = new List<string>();

				var subcategoryinfo = await _unitOfWork.SubCategory.GetAll().Where(sc => sc.Id == subCategoryId).Select(sc => new
                {
                    isdeleted=sc.DeletedAt!=null,
                    hasimage= sc.Images.Any(i=>i.Id==imageId&&i.DeletedAt==null),
                    countimage= sc.Images.Count(i=>i.DeletedAt==null),
                    isactive=sc.IsActive
                }).FirstOrDefaultAsync();

				if (subCategoryId <= 0 || imageId <= 0)
            {
                await transaction.RollbackAsync();
                return Result<bool>.Fail("Invalid subCategoryId or imageId.", 400);
            }
            if (subcategoryinfo == null)
            {
                await transaction.RollbackAsync();
                return Result<bool>.Fail($"SubCategory with id {subCategoryId} not found", 404);
            }

				

			
				if (!subcategoryinfo.hasimage )
				{
					await transaction.RollbackAsync();
					return Result<bool>.Fail("Image not found", 404);
				}

				var deleteResult = await _unitOfWork.Image.SoftDeleteAsync(imageId);
				if (!deleteResult)
				{
					_logger.LogError($"Failed to delete image file");
					await transaction.RollbackAsync();
					return Result<bool>.Fail("Can't Deleted Image",500);
				}

				

			
				if (subcategoryinfo.countimage<=1&&subcategoryinfo.isactive)
				{
					var result= await DeactivateSubCategoryAsync(subCategoryId,  userId);
				
					if (!result.Success)
					{
						await transaction.RollbackAsync();
						return Result<bool>.Fail("Can't Delete All Images it Become Deactive", 400);
					}
					warnings.Add("SubCategory was deactivated because it has no images left.");
				}

				var adminLog = await _adminopreationservices.AddAdminOpreationAsync(
					$"Remove Image {imageId} from SubCategory {subCategoryId}",
					Opreations.UpdateOpreation,
					userId,
					subCategoryId
				);

				if (!adminLog.Success)
				{
					_logger.LogWarning($"Failed to log admin operation: {adminLog.Message}");
				}

				await _unitOfWork.CommitAsync();
				await transaction.CommitAsync();

				RemoveSubCategoryCaches();

				
				
			

				return Result<bool>.Ok(true, "Image removed successfully", 200, warnings: warnings);
			}
			catch (Exception ex)
			{
				await transaction.RollbackAsync();
				_logger.LogError(ex, $"Unexpected error in RemoveImageFromSubCategoryAsync for subcategory {subCategoryId}");
				NotifyAdminOfError(ex.Message, ex.StackTrace);
				return Result<bool>.Fail("Unexpected error occurred while removing image", 500);
			}
		}


		public async Task<Result<List<SubCategoryDto>>> GetAllSubCategoriesAsync(bool? isActive = null, bool? isDeleted = null, int page = 1, int pageSize = 10)
        {
            _logger.LogInformation($"Executing {nameof(GetAllSubCategoriesAsync)} in SubCategoryService with isActive: {isActive}, isDeleted: {isDeleted}, page: {page}, pageSize: {pageSize}");
            var cacheKey = $"subcategory_all_{isActive}_{isDeleted}_{page}_{pageSize}";
            var cached = await _cacheManager.GetAsync<List<SubCategoryDto>>(cacheKey);
            if (cached != null)
            {
                _logger.LogInformation($"Cache hit for GetAllSubCategoriesAsync with key: {cacheKey}");
                return Result<List<SubCategoryDto>>.Ok(cached, "Subcategories fetched from cache", 200);
            }
            var subcategorise = await privateFilterSubCategoryAsync(string.Empty, isActive, isDeleted, page, pageSize);
            if (!subcategorise.Any())
                return Result<List<SubCategoryDto>>.Fail("No SubCategories Found");
          
            BackgroundJob.Enqueue(() => _cacheManager.SetAsync(cacheKey, subcategorise, null, new string[]{ CACHE_TAG_SUBCATEGORY }));
            return Result<List<SubCategoryDto>>.Ok(subcategorise, "Subcategories fetched", 200);
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
				
				if (subcategoryInfo==null)
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
				RemoveSubCategoryCaches();
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

				
				if (subcategoryInfo==null)
					return Result<bool>.Fail($"SubCategory with id {subCategoryId} not found", 404);

				if (!subcategoryInfo.IsActive){
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

				RemoveSubCategoryCaches();

			
				_backgroundJobClient.Enqueue(()=> DeactivateCategoryIfNoActiveSubcategories(subcategoryInfo.CategoryId, userId));

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


		private async Task DeactivateCategoryIfNoActiveSubcategories(int categoryId, string userId)
        {
            var check = await _unitOfWork.Category.HasSubCategoriesActiveAsync(categoryId);
                
            if (!check)
            {
                _logger.LogInformation($"All subcategories for category {categoryId} are inactive. Deactivating category.");
                await _categoryServices.DeactivateCategoryAsync(categoryId, userId ?? "system");
            }
        }
		public async Task DeactivateSubCategoryIfAllProductsAreInactiveAsync(int subCategoryId, string userId)
		{


            using var transaction =await _unitOfWork.BeginTransactionAsync();
			try
			{
				_logger.LogInformation($"Checking if subcategory {subCategoryId} needs to be deactivated.");

				var hasActiveProduct = await _unitOfWork.SubCategory.IsHasActiveProduct(subCategoryId);
				if (hasActiveProduct)
				{
					_logger.LogInformation($"Subcategory {subCategoryId} still has active products. No action taken.");
					return;
				}

				var isDeactivated = await _unitOfWork.SubCategory.DeActiveSubCategoryAsync(subCategoryId);
				if (!isDeactivated)
				{
					_logger.LogWarning($"SubCategory {subCategoryId} is already inactive.");
					await transaction.RollbackAsync();
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

				RemoveSubCategoryCaches();

				_backgroundJobClient.Enqueue(()=> DeactivateCategoryIfNoActiveSubcategories(subCategoryId, userId));
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, $"Error in DeactivateSubCategoryIfAllProductsAreInactiveAsync for subCategoryId: {subCategoryId}");
				await transaction.RollbackAsync(); 
				NotifyAdminOfError($"Exception in DeactivateSubCategoryIfAllProductsAreInactiveAsync for subcategory {subCategoryId}: {ex.Message}", ex.StackTrace);
				throw;
			}
		}



	}
} 