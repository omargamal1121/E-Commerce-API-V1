using AutoMapper;
using E_Commerce.DtoModels.CategoryDtos;
using E_Commerce.DtoModels.CollectionDtos;
using E_Commerce.DtoModels.ImagesDtos;
using E_Commerce.DtoModels.ProductDtos;
using E_Commerce.DtoModels.Responses;
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
using Microsoft.Extensions.Logging;
using System.Collections.Generic;
using System.Linq.Expressions;

namespace E_Commerce.Services.Collection
{
    public class CollectionServices :  ICollectionServices
    {
        private readonly ILogger<CollectionServices> _logger;
        private readonly IMapper _mapper;
        private readonly IUnitOfWork _unitOfWork;
		private readonly IErrorNotificationService _errorNotificationService;
		private readonly IImagesServices _imagesServices;
		private readonly ICollectionRepository _collectionRepository;
        private readonly IAdminOpreationServices _adminOperationServices;
        private readonly ICacheManager _cacheManager;
        private const string CACHE_TAG_COLLECTION = "collection";
		private const string CACHE_TAG_COLLECTION_With_Product = "CollectionWithProduct";
		private string[] TAGES = new string[] {CACHE_TAG_COLLECTION,CACHE_TAG_COLLECTION_With_Product };
		private  DateTime dateTime = DateTime.UtcNow;

        public CollectionServices(
			IErrorNotificationService errorNotificationService,
			 IImagesServices imagesServices,
			ILogger<CollectionServices> logger,
            IMapper mapper,
            IUnitOfWork unitOfWork,
            ICollectionRepository collectionRepository,
            IAdminOpreationServices adminOperationServices,
            ICacheManager cacheManager)
        {
			_errorNotificationService = errorNotificationService;
			_imagesServices = imagesServices;
            _logger = logger;
            _mapper = mapper;
            _unitOfWork = unitOfWork;
            _collectionRepository = collectionRepository;
            _adminOperationServices = adminOperationServices;
            _cacheManager = cacheManager;
        }
		private void RemoveCache()
		{
			_= _cacheManager.RemoveByTagsAsync(TAGES);
		}
		public async Task CheckAndDeactivateEmptyCollectionsAsync(int productId)
		{
				using var transaction = await _unitOfWork.BeginTransactionAsync();
			try
			{

				var collectionIdsToDeactivate = await _unitOfWork.Repository<E_Commerce.Models.Collection>()
					.GetAll()
					.Where(c => c.DeletedAt == null &&
								c.ProductCollections.Any(pc => pc.ProductId == productId) &&
								!c.ProductCollections.Any(pc =>
									pc.ProductId != productId && pc.Product.IsActive && pc.Product.DeletedAt == null))
					.Select(c => c.Id)
					.ToListAsync();

				if (collectionIdsToDeactivate.Any())
				{
					var collections = await _unitOfWork.Repository<E_Commerce.Models.Collection>()
						.GetAll()
						.Include(c => c.ProductCollections)
							.ThenInclude(pc => pc.Product)
						.Where(c => collectionIdsToDeactivate.Contains(c.Id))
						.ToListAsync();

					foreach (var collection in collections)
					{
						if (!collection.ProductCollections.Any(pc => pc.Product.IsActive && pc.Product.DeletedAt == null))
						{
							_logger.LogInformation($"Deactivating collection {collection.Id} because all products are inactive");
							collection.IsActive = false;
							collection.ModifiedAt = DateTime.UtcNow;
						}
						else
						{
							_logger.LogWarning($"Collection {collection.Id} not deactivated: still has active products");
						}
					}

					_unitOfWork.Repository<E_Commerce.Models.Collection>().UpdateList(collections);
					await _unitOfWork.CommitAsync();
					await transaction.CommitAsync();
				}
				else
				{
					_logger.LogInformation($"No collections meet deactivation criteria for productId: {productId}");
				}
			}
			catch (Exception ex)
			{
				await transaction.RollbackAsync(); 
				_logger.LogError(ex, $"Failed to deactivate collections for product {productId}");
				await _errorNotificationService.SendErrorNotificationAsync(ex.Message, ex.StackTrace);
				throw;
			}
		}





		public async Task<Result<List<ImageDto>>> AddImagesToCollectionAsync(int collectionid, List<IFormFile> images, string userId)
		{
			_logger.LogInformation($"Executing {nameof(AddImagesToCollectionAsync)} for categoryId: {collectionid}");
			if (images == null || !images.Any())
			{
				return Result<List<ImageDto>>.Fail("At least one image is required.", 400);
			}

			using var transaction = await _unitOfWork.BeginTransactionAsync();
			try
			{
				var collection = await _unitOfWork.Collection.IsExsistAsync(collectionid);
				if (!collection)
				{
					await transaction.RollbackAsync();
					return Result<List<ImageDto>>.Fail($"collection with id {collectionid} not found", 404);
				}

				var imageResult = await _imagesServices.SaveCollectionImagesAsync(images,collectionid, userId);
				if (!imageResult.Success || imageResult.Data == null)
				{
					await transaction.RollbackAsync();
					return Result<List<ImageDto>>.Fail($"Failed to save images: {imageResult.Message}", 400);
				}

			

			 _unitOfWork.Image.UpdateList(imageResult.Data);
				

				await _unitOfWork.CommitAsync();
				await transaction.CommitAsync();

				var mapped = _mapper.Map<List<ImageDto>>(imageResult.Data);
			RemoveCache();
				return Result<List<ImageDto>>.Ok(mapped, $"Added {imageResult.Data.Count} images to collection", 200, warnings: imageResult.Warnings);
			}
			catch (Exception ex)
			{
				await transaction.RollbackAsync();
				_logger.LogError($"Exception in AddImagesToCollectionAsync: {ex.Message}");
				NotifyAdminOfError($"Exception in AddImagesToCollectionAsync: {ex.Message}", ex.StackTrace);
				return Result<List<ImageDto>>.Fail("An error occurred while adding images", 500);
			}
		}


		public async Task<Result<ImageDto>> AddMainImageToCollectionAsync(int collectionid, IFormFile mainImage, string userId)
		{
			_logger.LogInformation($"Executing {nameof(AddMainImageToCollectionAsync)} for collectionid: {collectionid}");
			if (mainImage == null || mainImage.Length == 0)
			{
				return Result<ImageDto>.Fail("Main image is required.", 400);
			}

			using var transaction = await _unitOfWork.BeginTransactionAsync();
			try
			{
				var category = await _unitOfWork.Collection.GetByIdAsync(collectionid);
				if (category == null||category.DeletedAt!=null)
				{
					await transaction.RollbackAsync();
					return Result<ImageDto>.Fail($"collection with id {collectionid} not found", 404);
				}


				var mainImageResult = await _imagesServices.SaveMainCollectionImageAsync(mainImage,collectionid, userId);
				if (!mainImageResult.Success || mainImageResult.Data == null)
				{
					await transaction.RollbackAsync();
					return Result<ImageDto>.Fail($"Failed to save main image: {mainImageResult.Message}", 500);
				}

			var updateResult    =_unitOfWork.Image.Update(mainImageResult.Data);
			
				if (!updateResult)
				{
					await transaction.RollbackAsync();
					return Result<ImageDto>.Fail($"Failed to update collection with main image", 500);
				}

				await _unitOfWork.CommitAsync();
				await transaction.CommitAsync();

				  await _cacheManager.RemoveByTagAsync(CACHE_TAG_COLLECTION);

				var mapped = _mapper.Map<ImageDto>(mainImageResult.Data);
				return Result<ImageDto>.Ok(mapped, "Main image added to collection", 200);
			}
			catch (Exception ex)
			{
				await transaction.RollbackAsync();
				_logger.LogError($"Exception in AddMainImageToCollectionAsync: {ex.Message}");
				NotifyAdminOfError($"Exception in AddMainImageToCollectionAsync: {ex.Message}", ex.StackTrace);
				return Result<ImageDto>.Fail("An error occurred while adding main image", 500);
			}
		}

		public async Task<Result<bool>> RemoveImageFromCollectionAsync(int categoryId, int imageId, string userId)
		{
			_logger.LogInformation($"Removing image {imageId} from category: {categoryId}");
			using var transaction = await _unitOfWork.BeginTransactionAsync();
			try
			{
				var collection = await _unitOfWork.Collection.GetByIdAsync(categoryId);
				if (collection == null|| collection.DeletedAt!=null)
				{
					await transaction.RollbackAsync();
					return Result<bool>.Fail($"Category with id {categoryId} not found", 404);
				}



				var image= await _unitOfWork.Image.GetByIdAsync(imageId);
				if(image==null)
				{
					_logger.LogWarning($"No image with this id {imageId}");
					return Result<bool>.Fail($"No image with this id {imageId}");
				}
				
				var updateResult = _unitOfWork.Image.Remove(image);
				if (!updateResult)
				{
					await transaction.RollbackAsync();
					return Result<bool>.Fail("Failed to remove image", 400);
				}
				// Log admin operation
				var adminLog = await _adminOperationServices.AddAdminOpreationAsync(
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

			RemoveCache();
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

		
		private void NotifyAdminOfError(string message, string? stackTrace = null)
        {
            BackgroundJob.Enqueue<IErrorNotificationService>(_ => _.SendErrorNotificationAsync(message, stackTrace));
        }
        private Expression<Func<E_Commerce.Models.Collection, CollectionSummaryDto>> maptocollectionsummarydto = c =>
        new CollectionSummaryDto
        {
            CreatedAt = c.CreatedAt,
            DeletedAt = c.DeletedAt,
            ModifiedAt = c.ModifiedAt,
            Description = c.Description,
            DisplayOrder = c.DisplayOrder,
            Id = c.Id,
            IsActive = c.IsActive,
            images = c.Images.Select(c => new DtoModels.ImagesDtos.ImageDto { Id = c.Id, IsMain = c.IsMain, Url = c.Url }),
            Name = c.Name,
            TotalProducts = c.ProductCollections.Count()

        };
        private Expression<Func<E_Commerce.Models.Collection, CollectionDto>> maptocollectiondto = c =>
        new CollectionDto
		{
            CreatedAt = c.CreatedAt,
            DeletedAt = c.DeletedAt,
            ModifiedAt = c.ModifiedAt,
            Description = c.Description,
            DisplayOrder = c.DisplayOrder,
            Id = c.Id,
            IsActive = c.IsActive,
            Images = c.Images.Select(c => new DtoModels.ImagesDtos.ImageDto { Id = c.Id, IsMain = c.IsMain, Url = c.Url }),
            Name = c.Name,
            TotalProducts = c.ProductCollections.Count(),
            Products = c.ProductCollections.Where(p=>p.Product.IsActive&&p.Product.DeletedAt==null).Select(p=> new DtoModels.ProductDtos.ProductDto {
				Id = p.ProductId,
				Name = p.Product.Name,
				Description = p.Product.Description,
				AvailableQuantity = p.Product.Quantity,
				Gender = p.Product.Gender,
				SubCategoryId = p.Product.SubCategoryId,
				Price = p.Product.Price,
				CreatedAt = p.CreatedAt,
				ModifiedAt = p.ModifiedAt,
				DeletedAt = p.DeletedAt,
				FinalPrice = (p.Product.Discount != null && p.Product.Discount.IsActive && (p.Product.Discount.DeletedAt == null) && (p.Product.Discount.EndDate > DateTime.UtcNow)) ? Math.Round(p.Product.Price - (((p.Product.Discount.DiscountPercent)/100) * p.Product.Price)) : p.Product.Price,
				fitType = p.Product.fitType,
				images = p.Product.Images.Where(i => i.DeletedAt == null).Select(i => new ImageDto { Id = i.Id, Url = i.Url }),
				EndAt = (p.Product.Discount != null && p.Product.Discount.IsActive && p.Product.Discount.EndDate > DateTime.UtcNow) && p.Product.Discount.IsActive ? p.Product.Discount.EndDate : null,
				DiscountName = (p.Product.Discount != null && p.Product.Discount.IsActive && p.Product.Discount.EndDate > DateTime.UtcNow) ? p.Product.Discount.Name : null,
				DiscountPrecentage = (p.Product.Discount != null && p.Product.Discount.IsActive && p.Product.Discount.EndDate > DateTime.UtcNow) ? p.Product.Discount.DiscountPercent : 0,
				IsActive = p.Product.IsActive,
			}).Where(p=>p.IsActive && p.DeletedAt == null)

		};
        private IQueryable<E_Commerce.Models.Collection>BasicFilter(IQueryable<E_Commerce.Models.Collection> query, bool? IsActive = null, bool? IsDeleted = null)
        {
            if(IsActive.HasValue)
                query=query.Where(x => x.IsActive==IsActive.Value);
            if (IsDeleted.HasValue)
            {
                if (IsDeleted.Value)
                    query = query.Where(q => q.DeletedAt != null);
                else
					query = query.Where(q => q.DeletedAt == null);


			}
            return query;
		}
        public async Task<Result<CollectionDto>> GetCollectionByIdAsync(int collectionId,bool? IsActive=null , bool? IsDeleted= null)
        {
            _logger.LogInformation($"Getting collection by ID: {collectionId}");

            var cacheKey = $"{CACHE_TAG_COLLECTION_With_Product}_id_{collectionId}_IsActive_{IsActive}_IsDeleted_{IsDeleted}";
            var cached = await _cacheManager.GetAsync<CollectionDto>(cacheKey);
            if (cached != null)
            {
                _logger.LogInformation($"Cache hit for collection {collectionId}");
                return Result<CollectionDto>.Ok(cached, "Collection retrieved from cache", 200);
            }

            try
            {
                var query =  _collectionRepository.GetAll().Where(c => c.Id == collectionId);
                query = BasicFilter(query, IsActive, IsDeleted);
                var collectionDto = await query.Select(maptocollectiondto).FirstOrDefaultAsync();
				if (collectionDto == null){
					_logger.LogInformation($"Collection with ID {collectionId} not found");

					return Result<CollectionDto>.Fail("Collection not found", 404);
                }



				await _cacheManager.SetAsync(cacheKey, collectionDto, tags: new[] { CACHE_TAG_COLLECTION });

                return Result<CollectionDto>.Ok(collectionDto, "Collection retrieved successfully", 200);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error getting collection {collectionId}: {ex.Message}");
                NotifyAdminOfError($"Error getting collection {collectionId}: {ex.Message}", ex.StackTrace);
                return Result<CollectionDto>.Fail("An error occurred while retrieving collection", 500);
            }
        }

     
      

      
        /// <summary>
        /// Creates a new collection if the name is unique. Rolls back and logs if any error occurs.
        /// </summary>
        public async Task<Result<CollectionSummaryDto>> CreateCollectionAsync(CreateCollectionDto collectionDto, string userid)
        {
            _logger.LogInformation($"Creating collection: {collectionDto.Name}");
            using var transaction = await _unitOfWork.BeginTransactionAsync();
            try
            {
                var existingCollection = await _collectionRepository.IsExsistByName(collectionDto.Name);
                if (existingCollection)
                {
                    _logger.LogWarning($"Collection with name '{collectionDto.Name}' already exists.");
                    await transaction.RollbackAsync();
                    return Result<CollectionSummaryDto>.Fail("Collection with this name already exists", 400);
                }
                var collection = new Models.Collection {
                    Name= collectionDto.Name,
                    Description = collectionDto.Description?.Trim(),
                    DisplayOrder = collectionDto.DisplayOrder,
                    IsActive = false,
                };
                var createdCollection = await _collectionRepository.CreateAsync(collection);
                if (createdCollection == null)
                {
                    _logger.LogError($"Failed to create collection '{collectionDto.Name}'.");
                    await transaction.RollbackAsync();
                    return Result<CollectionSummaryDto>.Fail("Failed to create collection", 500);
                }
                await _unitOfWork.CommitAsync();
                // Log admin operation
                var adminLog = await _adminOperationServices.AddAdminOpreationAsync(
                    $"Created collection '{collectionDto.Name}'",
                    Enums.Opreations.AddOpreation,
                    userid,
                    createdCollection.Id
                );
                if (!adminLog.Success)
                {
                    _logger.LogWarning($"Failed to log admin operation: {adminLog.Message}");
                }
                await _unitOfWork.CommitAsync();
                await transaction.CommitAsync();
                RemoveCache();
                var collectionDtoResult = new CollectionSummaryDto {
                    Name = collection.Name,
                    Description = collection.Description,
                    CreatedAt=  collection.CreatedAt,
                    DeletedAt= collection.DeletedAt,
                    DisplayOrder = collection.DisplayOrder,
                    IsActive= collection.IsActive,
                    Id =collection.Id,
                    ModifiedAt =collection.ModifiedAt,
                    TotalProducts=0
                };
                return Result<CollectionSummaryDto>.Ok(collectionDtoResult, "Collection created successfully", 201);
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, $"Error creating collection: {collectionDto.Name}");
                NotifyAdminOfError($"Error creating collection: {collectionDto.Name}: {ex.Message}", ex.StackTrace);
                return Result<CollectionSummaryDto>.Fail("An error occurred while creating collection", 500);
            }
        }

        /// <summary>
        /// Updates an existing collection if found and not deleted. Logs changes and reasons for inaction.
        /// </summary>
        public async Task<Result<CollectionSummaryDto>> UpdateCollectionAsync(int collectionId, UpdateCollectionDto collectionDto, string userid)
        {
            _logger.LogInformation($"Updating collection {collectionId}");
            using var transaction = await _unitOfWork.BeginTransactionAsync();
            try
            {
                var collection = await _collectionRepository.GetByIdAsync(collectionId);
                if (collection == null || collection.DeletedAt != null)
                {
                    _logger.LogWarning($"Collection {collectionId} not found or deleted.");
                    await transaction.RollbackAsync();
                    return Result<CollectionSummaryDto>.Fail("Collection not found", 404);
                }
                // Track changes
                var changes = new List<string>();
                var warnings = new List<string>();
                if (!string.IsNullOrWhiteSpace(collectionDto.Name) && collection.Name != collectionDto.Name.Trim())
                {
                    var isExist = await _collectionRepository.IsExsistByName(collectionDto.Name);
                    if (!isExist)
                    {
                        changes.Add($"Name: '{collection.Name}' → '{collectionDto.Name.Trim()}'");
                        collection.Name = collectionDto.Name.Trim();
                    }
                    else
                    {
                        warnings.Add("Name is already used. Choose another name.");
                        _logger.LogWarning($"Attempted to update collection {collectionId} to duplicate name '{collectionDto.Name}'");
                    }
                }
                if (!string.IsNullOrWhiteSpace(collectionDto.Description) && collection.Description != collectionDto.Description.Trim())
                {
                    changes.Add($"Description: '{collection.Description}' → '{collectionDto.Description.Trim()}'");
                    collection.Description = collectionDto.Description.Trim();
                }
                if (collectionDto.DisplayOrder.HasValue && collection.DisplayOrder != collectionDto.DisplayOrder)
                {
                    changes.Add($"DisplayOrder: '{collection.DisplayOrder}' → '{collectionDto.DisplayOrder}'");
                    collection.DisplayOrder = collectionDto.DisplayOrder.Value;
                }
                if (!changes.Any())
                {
                    _logger.LogInformation($"No changes detected for collection {collectionId}.");
                    await transaction.RollbackAsync();
                    return Result<CollectionSummaryDto>.Fail("No changes detected", 400);
                }
                collection.ModifiedAt = DateTime.UtcNow;
                var updated = _collectionRepository.Update(collection);
                if (!updated)
                {
                    _logger.LogError($"Failed to update collection {collectionId}.");
                    await transaction.RollbackAsync();
                    return Result<CollectionSummaryDto>.Fail("Failed to update collection", 500);
                }
                await _unitOfWork.CommitAsync();
                // Save admin operation log with details of what changed
                var logMessage = $"Updated collection '{collection.Name}'. Changes: {string.Join(", ", changes)}";
                var adminLog = await _adminOperationServices.AddAdminOpreationAsync(
                    logMessage,
                    Enums.Opreations.UpdateOpreation,
                    userid,
                    collectionId
                );
                if (!adminLog.Success)
                {
                    _logger.LogWarning($"Failed to log admin operation: {adminLog.Message}");
                }
                await _unitOfWork.CommitAsync();
                await transaction.CommitAsync();
                RemoveCache();
                var updatedCollectionWithDetails = await _collectionRepository.GetByIdAsync(collectionId);
                var collectionDtoResult = new CollectionSummaryDto
                {
                    Name = collection.Name,
                    Description = collection.Description,
                    CreatedAt = collection.CreatedAt,
                    DeletedAt = collection.DeletedAt,
                    DisplayOrder = collection.DisplayOrder,
                    IsActive = collection.IsActive,
                    Id = collection.Id,
                    ModifiedAt = collection.ModifiedAt,
                    TotalProducts = 0
                };
                return Result<CollectionSummaryDto>.Ok(collectionDtoResult, "Collection updated successfully", 200, warnings);
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, $"Error updating collection {collectionId}");
                NotifyAdminOfError($"Error updating collection {collectionId}: {ex.Message}", ex.StackTrace);
                return Result<CollectionSummaryDto>.Fail("An error occurred while updating collection", 500);
            }
        }

        /// <summary>
        /// Soft deletes a collection if found. Logs and notifies on error.
        /// </summary>
        public async Task<Result<bool>> DeleteCollectionAsync(int collectionId, string userid)
        {
            _logger.LogInformation($"Deleting collection {collectionId}");
            using var transaction = await _unitOfWork.BeginTransactionAsync();
            try
            {
                var collection = await _collectionRepository.GetByIdAsync(collectionId);
                if (collection == null)
                {
                    _logger.LogWarning($"Collection {collectionId} not found for deletion.");
                    await transaction.RollbackAsync();
                    return Result<bool>.Fail("Collection not found", 404);
                }
                if (!collection.IsActive)
                {
                    _logger.LogInformation($"Collection {collectionId} is already inactive.");
                }
                collection.IsActive = false;
                var deleteResult = await _collectionRepository.SoftDeleteAsync(collectionId);
                if (!deleteResult)
                {
                    _logger.LogError($"Failed to delete collection {collectionId}.");
                    await transaction.RollbackAsync();
                    return Result<bool>.Fail("Failed to delete collection", 500);
                }
                // Log admin operation
                var adminLog = await _adminOperationServices.AddAdminOpreationAsync(
                    $"Deleted collection '{collection.Name}'",
                    Enums.Opreations.DeleteOpreation,
                    userid,
                    collectionId
                );
                if (!adminLog.Success)
                {
                    _logger.LogWarning($"Failed to log admin operation: {adminLog.Message}");
                }
                await _unitOfWork.CommitAsync();
                await transaction.CommitAsync();
                RemoveCache();
                return Result<bool>.Ok(true, "Collection deleted successfully", 200);
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, $"Error deleting collection {collectionId}");
                NotifyAdminOfError($"Error deleting collection {collectionId}: {ex.Message}", ex.StackTrace);
                return Result<bool>.Fail("An error occurred while deleting collection", 500);
            }
        }

		public async Task<Result<bool>> AddProductsToCollectionAsync(int collectionId, AddProductsToCollectionDto productsDto, string userId)
		{
			_logger.LogInformation($"Adding {productsDto.ProductIds.Count} products to collection {collectionId}");

			using var transaction = await _unitOfWork.BeginTransactionAsync();
			try
			{
				var collection = await _collectionRepository.GetByIdAsync(collectionId);
				if (collection == null)
				{
					await transaction.RollbackAsync();
					return Result<bool>.Fail("Collection not found", 404);
				}

				if (productsDto.ProductIds == null || productsDto.ProductIds.Count == 0)
				{
					await transaction.RollbackAsync();
					return Result<bool>.Fail("No product IDs provided", 400);
				}

				var warningMessage = new List<string>();
				var validProductIds = new List<int>();

				foreach (var productId in productsDto.ProductIds)
				{
					var productExists = await _unitOfWork.Product.IsExsistAndActiveAsync(productId);
					if (!productExists)
					{
						warningMessage.Add($"Product with ID {productId} not found Or Not Acive");
					}
					else
					{
						validProductIds.Add(productId);
					}
				}

				if (validProductIds.Count == 0)
				{
					await transaction.RollbackAsync();
					return Result<bool>.Fail("No valid product IDs found", 400);
				}

				var addResult = await _collectionRepository.AddProductsToCollectionAsync(collectionId, validProductIds);
				if (!addResult)
				{
					await transaction.RollbackAsync();
					return Result<bool>.Fail("Failed to add products to collection", 500);
				}

			
				var addedProductsLog = $"Added products with IDs: {string.Join(", ", validProductIds)} to collection '{collection.Name}'";

				var adminLog = await _adminOperationServices.AddAdminOpreationAsync(
					addedProductsLog,
					Enums.Opreations.UpdateOpreation,
					userId,
					collectionId
				);

				if (!adminLog.Success)
				{
					await transaction.RollbackAsync();
					_logger.LogWarning($"Failed to log admin operation: {adminLog.Message}");
					return Result<bool>.Fail("Failed to add products to collection", 500);
				}

				await _unitOfWork.CommitAsync();
				await transaction.CommitAsync();

			RemoveCache();

				var successMessage = "Products added to collection successfully";
			

				return Result<bool>.Ok(true, successMessage, 200,warningMessage);
			}
			catch (Exception ex)
			{
				await transaction.RollbackAsync();
				_logger.LogError($"Error adding products to collection {collectionId}: {ex.Message}");
				NotifyAdminOfError($"Error adding products to collection {collectionId}: {ex.Message}", ex.StackTrace);
				return Result<bool>.Fail("An error occurred while adding products to collection", 500);
			}
		}


		public async Task<Result<bool>> RemoveProductsFromCollectionAsync(int collectionId, RemoveProductsFromCollectionDto productsDto, string userId)
		{
			_logger.LogInformation($"Removing {productsDto.ProductIds.Count} products from collection {collectionId}");

			using var transaction = await _unitOfWork.BeginTransactionAsync();
			try
			{
				if (productsDto.ProductIds == null || !productsDto.ProductIds.Any())
				{
					await transaction.RollbackAsync();
					return Result<bool>.Fail("No product IDs provided to remove", 400);
				}

				var collection = await _collectionRepository.GetByIdAsync(collectionId);
				if (collection == null)
				{
					await transaction.RollbackAsync();
					return Result<bool>.Fail("Collection not found", 404);
				}

				var removeResult = await _collectionRepository.RemoveProductsFromCollectionAsync(collectionId, productsDto.ProductIds);
				if (!removeResult)
				{
					await transaction.RollbackAsync();
					return Result<bool>.Fail("Failed to remove products from collection", 500);
				}

				var adminLog = await _adminOperationServices.AddAdminOpreationAsync(
					$"Removed {productsDto.ProductIds.Count} products from collection '{collection.Name}'",
					Enums.Opreations.UpdateOpreation, 
					userId,
					collectionId
				);

				if (!adminLog.Success)
				{
					_logger.LogWarning($"Failed to log admin operation: {adminLog.Message}");
				}

				await _unitOfWork.CommitAsync();
				await transaction.CommitAsync();

			RemoveCache();

				return Result<bool>.Ok(true, "Products removed from collection successfully", 200);
			}
			catch (Exception ex)
			{
				await transaction.RollbackAsync();
				_logger.LogError($"Error removing products from collection {collectionId}: {ex.Message}");
				NotifyAdminOfError($"Error removing products from collection {collectionId}: {ex.Message}", ex.StackTrace);
				return Result<bool>.Fail("An error occurred while removing products from collection", 500);
			}
		}


		public async Task<Result<bool>> ActivateCollectionAsync(int collectionId, string userId)
		{
			_logger.LogInformation($"Activating collection {collectionId} by user {userId}");
			var transaction = await _unitOfWork.BeginTransactionAsync();

			try
			{
			
				var collection = _collectionRepository.GetAll()
					.Where(c => c.Id == collectionId && c.DeletedAt==null&&!c.IsActive)
					.Select(c => new
					{
						HasImages = c.Images.Any(),
						HasProducts = c.ProductCollections.Select(pc=>pc.Product).Where(p=>p.IsActive&&p.DeletedAt==null).Any()
					})
					.FirstOrDefault();

				if (collection == null)
					return Result<bool>.Fail("Collection not found", 404);

				if (!collection.HasImages)
					return Result<bool>.Fail("Collection must have at least one image before activation", 400);

				if (!collection.HasProducts)
					return Result<bool>.Fail("Collection must have at least one product before activation", 400);
	
				var updated = await _collectionRepository.UpdateCollectionStatusAsync(collectionId, true);
				if (!updated){
					await transaction.RollbackAsync();
					return Result<bool>.Fail("Failed to activate collection", 500);
					}

			  var adminlog= await _adminOperationServices.AddAdminOpreationAsync(
					$"Activated Collection {collectionId}",
					Enums.Opreations.UpdateOpreation,
					userId,
					collectionId
				);
				if(adminlog==null||!adminlog.Success)
				{
					_logger.LogError(adminlog?.Message);
				 await	transaction.RollbackAsync();
					NotifyAdminOfError("Error while add admin opreation");
					Result<bool>.Fail("Error while Acive collection");

				}
				await _unitOfWork.CommitAsync();
				await transaction.CommitAsync();
				RemoveCache();

				return Result<bool>.Ok(true, "Collection activated successfully", 200);
			}
			catch (Exception ex)
			{
			 await	transaction.RollbackAsync();
				_logger.LogError($"Error activating collection {collectionId}: {ex.Message}");
				return Result<bool>.Fail("An error occurred while activating the collection", 500);
			}
		}


		public async Task<Result<bool>> DeactivateCollectionAsync(int collectionId, string userId)
		{
			_logger.LogInformation($"Deactivating collection {collectionId} by user {userId}");
			var transaction = await _unitOfWork.BeginTransactionAsync();

			try
			{
				var updated = await _collectionRepository.UpdateCollectionStatusAsync(collectionId, false);
				if (!updated)
					return Result<bool>.Fail("Failed to deactivate collection", 500);


				var adminLog = await _adminOperationServices.AddAdminOpreationAsync(
					$"Make Collection {collectionId} Deactive",
					Enums.Opreations.UpdateOpreation,
					userId,
					collectionId
				);
				if (adminLog == null || !adminLog.Success)
				{
					_logger.LogError(adminLog?.Message);
					await transaction.RollbackAsync();
					NotifyAdminOfError("Error while add admin opreation");
					Result<bool>.Fail("Error while Acive collection");

				}
			await	_unitOfWork.CommitAsync();
				await transaction.CommitAsync();
				RemoveCache();

				return Result<bool>.Ok(true, "Collection deactivated successfully", 200);
			}
			catch (Exception ex)
			{
				await transaction.RollbackAsync();
				_logger.LogError($"Error deactivating collection {collectionId}: {ex.Message}");
				return Result<bool>.Fail("An error occurred while deactivating the collection", 500);
			}
		}

		public async Task<Result<bool>> UpdateCollectionDisplayOrderAsync(int collectionId, int displayOrder, string userid)
		{
			_logger.LogInformation($"Updating display order of collection {collectionId} to {displayOrder}");

			using var transaction = await _unitOfWork.BeginTransactionAsync();
			try
			{
				var collection = await _collectionRepository.GetByIdAsync(collectionId);
				if (collection == null)
				{
					await transaction.RollbackAsync();
					return Result<bool>.Fail("Collection not found", 404);
				}

				var updateResult = await _collectionRepository.UpdateCollectionDisplayOrderAsync(collectionId, displayOrder);
				if (!updateResult)
				{
					await transaction.RollbackAsync();
					return Result<bool>.Fail("Failed to update collection display order", 500);
				}

				
				var adminLog = await _adminOperationServices.AddAdminOpreationAsync(
					$"Updated display order of collection '{collection.Name}' to {displayOrder}",
					Enums.Opreations.UpdateOpreation,
					userid,
					collectionId
				);

				if (!adminLog.Success)
				{
					_logger.LogWarning($"Failed to log admin operation: {adminLog.Message}");
				}

				await _unitOfWork.CommitAsync();
				await transaction.CommitAsync();

				// Clear related cache
			RemoveCache();

				return Result<bool>.Ok(true, "Collection display order updated successfully", 200);
			}
			catch (Exception ex)
			{
				await transaction.RollbackAsync();
				_logger.LogError($"Error updating collection display order for collection {collectionId}: {ex.Message}");
				NotifyAdminOfError($"Error updating display order for collection {collectionId}: {ex.Message}", ex.StackTrace);
				return Result<bool>.Fail("An error occurred while updating collection display order", 500);
			}
		}

		public async Task<Result<List<CollectionSummaryDto>>> SearchCollectionsAsync(string? searchTerm,bool? IsActive=null, bool?IsDeleted=null,int page=1, int pagesize=10)
        {
            _logger.LogInformation($"Searching collections with term: {searchTerm}");

            try
            {
                var query =  _collectionRepository.GetCollectionsByName(searchTerm,IsActive,IsDeleted); var collectionDtos = await query
	.Select(maptocollectionsummarydto)
	.OrderBy(x => x.DisplayOrder)
	.ThenBy(x => x.CreatedAt)
	.Skip((page - 1) * pagesize)
	.Take(pagesize)
	.ToListAsync();
				return Result<List<CollectionSummaryDto>>.Ok(collectionDtos, "Collections search completed successfully", 200);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error searching collections: {ex.Message}");
                NotifyAdminOfError($"Error searching collections: {ex.Message}", ex.StackTrace);
                return Result<List<CollectionSummaryDto>>.Fail("An error occurred while searching collections", 500);
            }
        }

       

     
    }
} 