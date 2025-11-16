
using ApplicationLayer.DtoModels.ImagesDtos;
using ApplicationLayer.Interfaces;
using ApplicationLayer.Services.AdminOperationServices;
using ApplicationLayer.Services.EmailServices;
using DomainLayer.Enums;
using Hangfire;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ApplicationLayer.Services.CategoryServices
{
	public class CategoryImageServices:ICategoryImageService
	{
		private readonly IUnitOfWork _unitOfWork;
		private readonly IImagesServices _imagesServices;
		private readonly IAdminOpreationServices _adminopreationservices;
		private readonly ICategoryCacheHelper _categoryCacheHelper;
		private readonly ICategoryMapper _mapping;
		private readonly ICategoryCommandService _categoryCommandService;
		private readonly ILogger<CategoryImageServices> _logger;
		private readonly IBackgroundJobClient _backgroundJobClient;

		public CategoryImageServices(


			ICategoryCommandService categoryCommandService,
			IBackgroundJobClient backgroundJobClient,
			ICategoryCacheHelper categoryCacheHelper,
			IUnitOfWork unitOfWork,
			IImagesServices imagesServices,
			IAdminOpreationServices adminopreationservices,
			ICategoryMapper mapping,
			ILogger<CategoryImageServices> logger


			)
		{
			_categoryCommandService = categoryCommandService;
			_categoryCacheHelper = categoryCacheHelper;
			_unitOfWork = unitOfWork;
			_backgroundJobClient = backgroundJobClient;
			_imagesServices = imagesServices;
			_adminopreationservices = adminopreationservices;
			_mapping = mapping;
			_logger = logger;
		}
		private void NotifyAdminOfError(string message, string? stackTrace = null)
		{
			_backgroundJobClient.Enqueue<IErrorNotificationService>(_ => _.SendErrorNotificationAsync(message, stackTrace));
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


				var adminLog = await _adminopreationservices.AddAdminOpreationAsync(

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

				var mapped = imageResult.Data.Select(i => new ImageDto { Id = i.Id, Url = i.Url, IsMain = i.IsMain }).ToList();
				_categoryCacheHelper.ClearCategoryCache();

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

				var mainImageResult = await _imagesServices.SaveMainCategoryImageAsync(mainImage, categoryId, userId);
				if (!mainImageResult.Success || mainImageResult.Data == null)
				{
					await transaction.RollbackAsync();
					return Result<ImageDto>.Fail($"Failed to save main image: {mainImageResult.Message}", 500);
				}

				await _unitOfWork.CommitAsync();
				await transaction.CommitAsync();

				_categoryCacheHelper.ClearCategoryCache();

				var mapped = new ImageDto { Id = mainImageResult.Data.Id, Url = mainImageResult.Data.Url, IsMain = mainImageResult.Data.IsMain };

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

				 var isdeleted= await _imagesServices.DeleteImageAsync(imageId);
				if(!isdeleted.Success)
				{
					return Result<bool>.Fail(isdeleted.Message, isdeleted.StatusCode);
				}
				


				int remainingImages = categoryData.ImagesCount - 1;

				if (remainingImages == 0 && categoryData.IsActive)
				{
					var result = await _categoryCommandService.DeactivateAsync(categoryId, userId);
					if (!result.Success)
					{
						await transaction.RollbackAsync();
						return Result<bool>.Fail("Can't Delete All Imags It Become Deactive", 400);
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
				_categoryCacheHelper.ClearCategoryCache();

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


