using E_Commerce.DtoModels.CollectionDtos;
using E_Commerce.DtoModels.Responses;
using E_Commerce.Enums;
using E_Commerce.ErrorHnadling;
using E_Commerce.Interfaces;
using E_Commerce.Models;
using E_Commerce.Services.EmailServices;
using E_Commerce.UOW;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Hangfire;
using E_Commerce.Services.AdminOperationServices;

namespace E_Commerce.Services.Collection
{
    public class CollectionCommandService : ICollectionCommandService
    {
        private readonly ILogger<CollectionCommandService> _logger;
        private readonly IUnitOfWork _unitOfWork;
        private readonly IErrorNotificationService _errorNotificationService;
        private readonly ICollectionRepository _collectionRepository;
        private readonly IAdminOpreationServices _adminOperationServices;
        private readonly ICollectionCacheHelper _cacheHelper;
        private readonly ICollectionMapper _mapper;

        public CollectionCommandService(
            IErrorNotificationService errorNotificationService,
            ILogger<CollectionCommandService> logger,
            IUnitOfWork unitOfWork,
            ICollectionRepository collectionRepository,
            IAdminOpreationServices adminOperationServices,
            ICollectionCacheHelper cacheHelper,
            ICollectionMapper mapper)
        {
            _errorNotificationService = errorNotificationService;
            _logger = logger;
            _unitOfWork = unitOfWork;
            _collectionRepository = collectionRepository;
            _adminOperationServices = adminOperationServices;
            _cacheHelper = cacheHelper;
            _mapper = mapper;
        }

		public async Task DeactivateCollectionsWithoutActiveProductsAsync(int productId)
		{
			using var transaction = await _unitOfWork.BeginTransactionAsync();
			try
			{
				var collections = await _unitOfWork.Repository<E_Commerce.Models.Collection>()
					.GetAll()
					.Include(c => c.ProductCollections)
						.ThenInclude(pc => pc.Product)
					.Where(c => c.DeletedAt == null &&
								c.ProductCollections.Any(pc => pc.ProductId == productId))
					.ToListAsync();

				if (!collections.Any())
				{
					_logger.LogInformation("No collections found for productId: {ProductId}", productId);
					return;
				}

				var collectionsToDeactivate = collections
					.Where(c => !c.ProductCollections.Any(pc =>
								pc.ProductId != productId && pc.Product.IsActive && pc.Product.DeletedAt == null))
					.ToList();

				foreach (var collection in collectionsToDeactivate)
				{
					if (!collection.ProductCollections.Any(pc => pc.Product.IsActive && pc.Product.DeletedAt == null))
					{
						_logger.LogInformation("Deactivating collection {CollectionId}: no active products remain", collection.Id);
						collection.IsActive = false;
						collection.ModifiedAt = DateTime.UtcNow;
					}
					else
					{
						_logger.LogInformation("Collection {CollectionId} still has active products, skipping deactivation", collection.Id);
					}
				}

				if (collectionsToDeactivate.Any())
				{
					_unitOfWork.Repository<E_Commerce.Models.Collection>().UpdateList(collectionsToDeactivate);
					await _unitOfWork.CommitAsync();
				}
				
				await transaction.CommitAsync();
			}
			catch (Exception ex)
			{
				await transaction.RollbackAsync();
				_logger.LogError(ex, "Failed to deactivate collections for productId {ProductId}", productId);
				await _errorNotificationService.SendErrorNotificationAsync(ex.Message, ex.StackTrace);
				throw;
			}
		}

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
                var collection = new Models.Collection
                {
                    Name = collectionDto.Name,
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
                _cacheHelper.ClearCollectionCache();
                var collectionDtoResult = _mapper.ToCollectionSummaryDto(createdCollection);
                return Result<CollectionSummaryDto>.Ok(collectionDtoResult, "Collection created successfully", 201);
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, $"Error creating collection: {collectionDto.Name}");
                _cacheHelper.NotifyAdminError($"Error creating collection: {collectionDto.Name}: {ex.Message}", ex.StackTrace);
                return Result<CollectionSummaryDto>.Fail("An error occurred while creating collection", 500);
            }
        }

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
                _cacheHelper.ClearCollectionCache();
                var collectionDtoResult = _mapper.ToCollectionSummaryDto(collection);
                return Result<CollectionSummaryDto>.Ok(collectionDtoResult, "Collection updated successfully", 200, warnings);
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, $"Error updating collection {collectionId}");
                _cacheHelper.NotifyAdminError($"Error updating collection {collectionId}: {ex.Message}", ex.StackTrace);
                return Result<CollectionSummaryDto>.Fail("An error occurred while updating collection", 500);
            }
        }

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
                _cacheHelper.ClearCollectionCache();
                return Result<bool>.Ok(true, "Collection deleted successfully", 200);
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, $"Error deleting collection {collectionId}");
                _cacheHelper.NotifyAdminError($"Error deleting collection {collectionId}: {ex.Message}", ex.StackTrace);
                return Result<bool>.Fail("An error occurred while deleting collection", 500);
            }
        }

        public async Task<Result<bool>> ActivateCollectionAsync(int collectionId, string userId)
        {
            _logger.LogInformation($"Activating collection {collectionId} by user {userId}");
            var transaction = await _unitOfWork.BeginTransactionAsync();

            try
            {
                var collection = _collectionRepository.GetAll()
                    .Where(c => c.Id == collectionId && c.DeletedAt == null && !c.IsActive)
                    .Select(c => new
                    {
                        HasImages = c.Images.Any(),
                        HasProducts = c.ProductCollections.Select(pc => pc.Product).Where(p => p.IsActive && p.DeletedAt == null).Any()
                    })
                    .FirstOrDefault();

                if (collection == null)
                    return Result<bool>.Fail("Collection not found", 404);

                if (!collection.HasImages)
                    return Result<bool>.Fail("Collection must have at least one image before activation", 400);

                if (!collection.HasProducts)
                    return Result<bool>.Fail("Collection must have at least one product before activation", 400);

                var updated = await _collectionRepository.UpdateCollectionStatusAsync(collectionId, true);
                if (!updated)
                {
                    await transaction.RollbackAsync();
                    return Result<bool>.Fail("Failed to activate collection", 500);
                }

                var adminlog = await _adminOperationServices.AddAdminOpreationAsync(
                    $"Activated Collection {collectionId}",
                    Enums.Opreations.UpdateOpreation,
                    userId,
                    collectionId
                );
                if (adminlog == null || !adminlog.Success)
                {
                    _logger.LogError(adminlog?.Message);
                    await transaction.RollbackAsync();
                    _cacheHelper.NotifyAdminError("Error while add admin operation");
                    return Result<bool>.Fail("Error while activating collection", 500);
                }
                await _unitOfWork.CommitAsync();
                await transaction.CommitAsync();
                _cacheHelper.ClearCollectionCache();

                return Result<bool>.Ok(true, "Collection activated successfully", 200);
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
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
                    _cacheHelper.NotifyAdminError("Error while add admin operation");
                    return Result<bool>.Fail("Error while deactivating collection", 500);
                }
                await _unitOfWork.CommitAsync();
                await transaction.CommitAsync();
                _cacheHelper.ClearCollectionCache();

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

                collection.DisplayOrder = displayOrder;
                collection.ModifiedAt = DateTime.UtcNow;

                var updateResult = _collectionRepository.Update(collection);
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
                _cacheHelper.ClearCollectionCache();

                return Result<bool>.Ok(true, "Collection display order updated successfully", 200);
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError($"Error updating collection display order for collection {collectionId}: {ex.Message}");
                _cacheHelper.NotifyAdminError($"Error updating display order for collection {collectionId}: {ex.Message}", ex.StackTrace);
                return Result<bool>.Fail("An error occurred while updating collection display order", 500);
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

                if (productsDto.ProductIds == null || !productsDto.ProductIds.Any())
                {
                    await transaction.RollbackAsync();
                    return Result<bool>.Fail("No product IDs provided", 400);
                }

                var warningMessage = new List<string>();
                var validProductIds = new List<int>();

                // Batch validation for better performance
                var productIds = productsDto.ProductIds.ToList();
                var existingProducts = await _unitOfWork.Product.GetAll()
                    .Where(p => productIds.Contains(p.Id) && p.IsActive && p.DeletedAt == null)
                    .Select(p => p.Id)
                    .ToListAsync();

                foreach (var productId in productIds)
                {
                    if (!existingProducts.Contains(productId))
                    {
                        warningMessage.Add($"Product with ID {productId} not found or not active");
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

                _cacheHelper.ClearCollectionCache();

                var successMessage = "Products added to collection successfully";

                return Result<bool>.Ok(true, successMessage, 200, warningMessage);
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError($"Error adding products to collection {collectionId}: {ex.Message}");
                _cacheHelper.NotifyAdminError($"Error adding products to collection {collectionId}: {ex.Message}", ex.StackTrace);
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

                _cacheHelper.ClearCollectionCache();

                return Result<bool>.Ok(true, "Products removed from collection successfully", 200);
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError($"Error removing products from collection {collectionId}: {ex.Message}");
                _cacheHelper.NotifyAdminError($"Error removing products from collection {collectionId}: {ex.Message}", ex.StackTrace);
                return Result<bool>.Fail("An error occurred while removing products from collection", 500);
            }
        }
    }
}
