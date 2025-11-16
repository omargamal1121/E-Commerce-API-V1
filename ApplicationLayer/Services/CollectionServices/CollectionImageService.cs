

using System.Linq;


using ApplicationLayer.DtoModels.CollectionDtos;
using ApplicationLayer.DtoModels.ImagesDtos;
using ApplicationLayer.DtoModels.Responses;
using ApplicationLayer.ErrorHnadling;
using ApplicationLayer.Interfaces;
using ApplicationLayer.Services.AdminOperationServices;
using ApplicationLayer.Services.Cache;
using ApplicationLayer.Services.EmailServices;
using DomainLayer.Enums;
using Hangfire;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace ApplicationLayer.Services.CollectionServices
{
    public class CollectionImageService : ICollectionImageService
    {
        private readonly ILogger<CollectionImageService> _logger;
        private readonly IUnitOfWork _unitOfWork;
        private readonly IErrorNotificationService _errorNotificationService;
        private readonly IImagesServices _imagesServices;
        private readonly IAdminOpreationServices _adminOperationServices;
        private readonly ICollectionCacheHelper _collectionCacheHelper;
        public CollectionImageService(
            ICollectionCacheHelper collectionCacheHelper,
            IErrorNotificationService errorNotificationService,
            IImagesServices imagesServices,
            ILogger<CollectionImageService> logger,
            IUnitOfWork unitOfWork,
            IAdminOpreationServices adminOperationServices
           )
        {
            _collectionCacheHelper = collectionCacheHelper;
            _errorNotificationService = errorNotificationService;
            _imagesServices = imagesServices;
            _logger = logger;
  
            _unitOfWork = unitOfWork;
            _adminOperationServices = adminOperationServices;
        }

        
        private void NotifyAdminOfError(string message, string? stackTrace = null)
        {
            BackgroundJob.Enqueue<IErrorNotificationService>(_ => _.SendErrorNotificationAsync(message, stackTrace));
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

                var imageResult = await _imagesServices.SaveCollectionImagesAsync(images, collectionid, userId);
                if (!imageResult.Success || imageResult.Data == null)
                {
                    await transaction.RollbackAsync();
                    return Result<List<ImageDto>>.Fail($"Failed to save images: {imageResult.Message}", 400);
                }

                _unitOfWork.Image.UpdateList(imageResult.Data);

                await _unitOfWork.CommitAsync();
                await transaction.CommitAsync();

                var mapped = imageResult.Data.Select(img => new ImageDto
                {
                    Id = img.Id,
                    Url = img.Url,
                    IsMain = img.IsMain
                }).ToList();
                _collectionCacheHelper.ClearCollectionCache();
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
                if (category == null || category.DeletedAt != null)
                {
                    await transaction.RollbackAsync();
                    return Result<ImageDto>.Fail($"collection with id {collectionid} not found", 404);
                }

                var mainImageResult = await _imagesServices.SaveMainCollectionImageAsync(mainImage, collectionid, userId);
                if (!mainImageResult.Success || mainImageResult.Data == null)
                {
                    await transaction.RollbackAsync();
                    return Result<ImageDto>.Fail($"Failed to save main image: {mainImageResult.Message}", 500);
                }

                var updateResult = _unitOfWork.Image.Update(mainImageResult.Data);

                if (!updateResult)
                {
                    await transaction.RollbackAsync();
                    return Result<ImageDto>.Fail($"Failed to update collection with main image", 500);
                }

                await _unitOfWork.CommitAsync();
                await transaction.CommitAsync();

				_collectionCacheHelper.ClearCollectionCache();


				var mapped = new ImageDto
                {
                    Id = mainImageResult.Data.Id,
                    Url = mainImageResult.Data.Url,
                    IsMain = mainImageResult.Data.IsMain
                };
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
                if (collection == null || collection.DeletedAt != null)
                {
                    await transaction.RollbackAsync();
                    return Result<bool>.Fail($"Category with id {categoryId} not found", 404);
                }

                var image = await _unitOfWork.Image.GetByIdAsync(imageId);
                if (image == null)
                {
                    _logger.LogWarning($"No image with this id {imageId}");
                    return Result<bool>.Fail($"No image with this id {imageId}");
                }

                var updateResult = await _imagesServices.DeleteImageAsync(image);
                if (!updateResult.Success)
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

                                _collectionCacheHelper.ClearCollectionCache();
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


