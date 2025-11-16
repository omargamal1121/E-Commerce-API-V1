using DomainLayer.Enums;
using ApplicationLayer.DtoModels.ImagesDtos;
using ApplicationLayer.Interfaces;
using DomainLayer.Models;
using ApplicationLayer.Services;
using ApplicationLayer.Services.AdminOperationServices;
using ApplicationLayer.Services.CategoryServices;
using ApplicationLayer.Services.EmailServices;

using Hangfire;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Collections.Generic;
using Microsoft.AspNetCore.Http;

namespace ApplicationLayer.Services.SubCategoryServices
{
    public class SubCategoryImageService : ISubCategoryImageService
    {
        private readonly IUnitOfWork _unitOfWork;
		private readonly ICategoryCacheHelper _categoryCacheHelper;
		private readonly ILogger<SubCategoryImageService> _logger;
        private readonly IAdminOpreationServices _adminOpreationServices;
        private readonly IBackgroundJobClient _backgroundJobClient;
        private readonly ISubCategoryCacheHelper _subCategoryCacheHelper;
		private readonly ISubCategoryCommandService _subCategoryCommandService;
		private readonly IImagesServices _imagesServices; 

        public SubCategoryImageService(
			ICategoryCacheHelper categoryCacheHelper,
			ISubCategoryCommandService subCategoryCommandService,
			IUnitOfWork unitOfWork,
            ILogger<SubCategoryImageService> logger,
            IAdminOpreationServices adminOpreationServices,
            IBackgroundJobClient backgroundJobClient,
            ISubCategoryCacheHelper subCategoryCacheHelper,
            IImagesServices imagesServices)
        {
			_categoryCacheHelper = categoryCacheHelper;
			_subCategoryCommandService = subCategoryCommandService;
			_unitOfWork = unitOfWork;
            _logger = logger;
            _adminOpreationServices = adminOpreationServices;
            _backgroundJobClient = backgroundJobClient;
            _subCategoryCacheHelper = subCategoryCacheHelper;
            _imagesServices = imagesServices;
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

				var mainImageResult = await _imagesServices.SaveMainSubCategoryImageAsync(mainImage, subCategoryId, userId);
				if (mainImageResult == null || !mainImageResult.Success || mainImageResult.Data == null)
				{
					await transaction.RollbackAsync();
					return Result<ImageDto>.Fail(mainImageResult?.Message ?? "Failed to save main image", mainImageResult?.StatusCode ?? 500, mainImageResult?.Warnings);
				}



				var adminLog = await _adminOpreationServices.AddAdminOpreationAsync(
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
				_subCategoryCacheHelper.ClearSubCategoryCache();
				_categoryCacheHelper.ClearCategoryDataCache();
				var mapped = new ImageDto
				{
					Id = mainImageResult.Data.Id,
					Url = mainImageResult.Data.Url,
					IsMain = mainImageResult.Data.IsMain
				};
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

		public async Task<Result<List<ImageDto>>> GetSubCategoryImagesAsync(int subCategoryId)
        {
            _logger.LogInformation($"Execute {nameof(GetSubCategoryImagesAsync)} for SubCategory ID: {subCategoryId}");

            var imageDtos = await _unitOfWork.Image
                .GetAll()
                .Where(img => img.SubCategoryId == subCategoryId&&img.DeletedAt==null).Select(img => new ImageDto
				{
					Id = img.Id,
					Url = img.Url,
					IsMain = img.IsMain
				}).ToListAsync();

			if (imageDtos == null)
            {
                _logger.LogWarning($"SubCategory {subCategoryId} not found.");
                return Result<List<ImageDto>>.Fail($"SubCategory with ID {subCategoryId} not found", 404);
            }

            return Result<List<ImageDto>>.Ok(imageDtos);
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
				var imageResult = await _imagesServices.SaveSubCategoryImagesAsync(images, subCategoryId, userId);
				if (imageResult == null || imageResult.Data == null)
				{
					await transaction.RollbackAsync();
					return Result<List<ImageDto>>.Fail(imageResult?.Message ?? "Failed to save images", imageResult?.StatusCode ?? 500, imageResult?.Warnings);
				}



				var adminLog = await _adminOpreationServices.AddAdminOpreationAsync(
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
				_subCategoryCacheHelper.ClearSubCategoryCache();
				_categoryCacheHelper.ClearCategoryDataCache();

				var imagesdto= imageResult.Data.Select(img => new ImageDto
                {
                    Id = img.Id,
                    Url = img.Url,
                    IsMain = img.IsMain
                }).ToList();
				return Result<List<ImageDto>>.Ok(imagesdto, $"Added {imageResult.Data.Count} images to subcategory", 200, warnings: imageResult.Warnings);
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, $"Exception in {nameof(AddImagesToSubCategoryAsync)} for subCategoryId: {subCategoryId}");
				await transaction.RollbackAsync();
				NotifyAdminOfError(ex.Message, ex.StackTrace);
				return Result<List<ImageDto>>.Fail("Unexpected error occurred while adding images", 500);
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
					isdeleted = sc.DeletedAt != null,
					hasimage = sc.Images.Any(i => i.Id == imageId && i.DeletedAt == null),
					countimage = sc.Images.Count(i => i.DeletedAt == null),
					isactive = sc.IsActive
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




				if (!subcategoryinfo.hasimage)
				{
					await transaction.RollbackAsync();
					return Result<bool>.Fail("Image not found", 404);
				}

				var deleteResult = await _imagesServices.DeleteImageAsync(imageId);
				if (!deleteResult.Success)
				{
					_logger.LogError($"Failed to delete image file");
					await transaction.RollbackAsync();
					return Result<bool>.Fail(deleteResult.Message,deleteResult.StatusCode);
				}




				if (subcategoryinfo.countimage <= 1 && subcategoryinfo.isactive)
				{
					var result = await _subCategoryCommandService.DeactivateSubCategoryAsync(subCategoryId, userId);

					if (!result.Success)
					{
						await transaction.RollbackAsync();
						return Result<bool>.Fail("Can't Delete All Images it Become Deactive", 400);
					}
					warnings.Add("SubCategory was deactivated because it has no images left.");
				}

				var adminLog = await _adminOpreationServices.AddAdminOpreationAsync(
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

				_subCategoryCacheHelper.ClearSubCategoryCache();
				_categoryCacheHelper.ClearCategoryDataCache();




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


		private void NotifyAdminOfError(string message, string? stackTrace = null)
        {
            _backgroundJobClient.Enqueue<IErrorNotificationService>(_ => _.SendErrorNotificationAsync(message, stackTrace));
        }
    }
}

