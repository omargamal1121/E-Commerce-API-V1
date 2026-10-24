using Application.DtoModels.ImageDtos;
using Application.Interfaces;
using Application.Services.AdminOperationServices;
using Application.Services.EmailServices;
using Domain.Enums;
using Domain.Models;
using Hangfire;
using Infrastructure.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Application.Services.ImageServices
{
    public class ImagesServices : IImagesServices
    {
        private readonly ILogger<ImagesServices> _logger;
        private readonly IConfiguration _configuration;
        private readonly IUnitOfWork _unitOfWork;
        private readonly IErrorNotificationService _errorNotificationService;
        private readonly IAdminOpreationServices _adminOpreationServices;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly IBackgroundJobClient _backgroundJobClient;
        private readonly IImageStorageService _imageStorageService;

        private int MaxFileSize => _configuration.GetValue<int>("Security:FileUpload:MaxFileSizeMB", 15) * 1024 * 1024;
        private string[] AllowedExtensions => _configuration.GetSection("Security:FileUpload:AllowedExtensions").Get<string[]>()
            ?? new[] { ".jpg", ".jpeg", ".png", ".webp" };

        public ImagesServices(
            IErrorNotificationService errorNotificationService,
            IBackgroundJobClient backgroundJobClient,
            IImageStorageService imageStorageService,
            IHttpContextAccessor httpContextAccessor,
            ILogger<ImagesServices> logger,
            IConfiguration configuration,
            IUnitOfWork unitOfWork,
            IAdminOpreationServices adminOpreationServices)
        {
            _errorNotificationService = errorNotificationService;
            _backgroundJobClient = backgroundJobClient;
            _imageStorageService = imageStorageService;
            _httpContextAccessor = httpContextAccessor;
            _logger = logger;
            _configuration = configuration;
            _unitOfWork = unitOfWork;
            _adminOpreationServices = adminOpreationServices;
        }

        public bool IsValidExtension(string extension) =>
            !string.IsNullOrWhiteSpace(extension) && AllowedExtensions.Contains(extension.ToLower());

        private async Task LogAdminOperationAsync(string userId, string description, int itemId)
        {
            try
            {
                await _adminOpreationServices.AddAdminOpreationAsync(description, Opreations.AddOpreation, userId, itemId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error logging admin operation");
            }
        }

        private void NotifyAdminOfError(string message, string? stackTrace = null)
        {
            _backgroundJobClient.Enqueue<IErrorNotificationService>(_ => _.SendErrorNotificationAsync(message, stackTrace));
        }

        private async Task<Result<Image>> SaveImageAsync(IFormFile image, string folderName, int id, bool isMain = false, string? userId = null)
        {
            _logger.LogInformation("Saving {ImageType} image to {Folder}", isMain ? "main" : "regular", folderName);

            if (image == null || image.Length == 0)
                return Result<Image>.Fail("Image is null or empty");

            //if (image.Length > MaxFileSize)
            //    return Result<Image>.Fail($"File size must not exceed {MaxFileSize / (1024 * 1024)}MB");

            string extension = Path.GetExtension(image.FileName);
            if (!IsValidExtension(extension))
                return Result<Image>.Fail($"Invalid extension. Allowed: {string.Join(", ", AllowedExtensions)}");

            try
            {
                await using var stream = image.OpenReadStream();

              
                ImageUploadResult uploadResult = await _imageStorageService.UploadAsync(stream, image.FileName, folderName);

                
                var savedImage = new Image
                {
                    CloudinaryPublicId = uploadResult.FileId,
                    UploadDate = DateTime.UtcNow,
                    Folder = folderName,
                    Url = uploadResult.Url,
                    FileSize = uploadResult.SizeInBytes,
                    FileType = "image/webp",
                    Width = uploadResult.Width,
                    Height = uploadResult.Height,
                    IsMain = isMain
                };

                // Set foreign key based on folder
                AssignImageToEntity(savedImage, folderName, id, userId);

                // Save to database
                var imageRepo = _unitOfWork.Repository<Image>();
                await imageRepo.CreateAsync(savedImage);
                await _unitOfWork.CommitAsync();

                // Log admin operation if userId provided
                if (!string.IsNullOrEmpty(userId))
                {
                    await LogAdminOperationAsync(userId,
                        $"Uploaded {(isMain ? "main" : "")} image to {folderName}",
                        savedImage.Id);
                }

                _logger.LogInformation("Image saved successfully: {Url}", savedImage.Url);
                return Result<Image>.Ok(savedImage);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving image to {Folder}", folderName);
                NotifyAdminOfError($"Error saving image: {ex.Message}", ex.StackTrace);
                return Result<Image>.Fail($"Error saving image: {ex.Message}", 500);
            }
        }

        private void AssignImageToEntity(Image image, string folderName, int id, string? userId)
        {
            switch (folderName.ToLower())
            {
                case "categoryphotos":
                    image.CategoryId = id;
                    break;
                case "productphotos":
                    image.ProductId = id;
                    break;
                case "subcategoryphotos":
                    image.SubCategoryId = id;
                    break;
                case "collectionphotos":
                    image.CollectionId = id;
                    break;
                case "customerphotos":
                    image.CustomerId = userId;
                    break;
                default:
                    _logger.LogWarning("Unknown folder name: {FolderName}", folderName);
                    break;
            }
        }

        private async Task<Result<List<Image>>> SaveImagesAsync(
            List<IFormFile> images, int id, string folderName, string userId)
        {
            _logger.LogInformation("Saving {Count} images to {Folder}", images?.Count, folderName);

            if (images == null || images.Count == 0)
                return Result<List<Image>>.Fail("Images are null or empty");

            var tasks = images.Select(img => SaveImageAsync(img, folderName, id, false, userId));
            var results = await Task.WhenAll(tasks);

            var savedImages = new List<Image>();
            var errors = new List<string>();

            for (int i = 0; i < results.Length; i++)
            {
                var result = results[i];
                if (!result.Success || result.Data == null)
                {
                    errors.Add($"Image #{i + 1}: {result.Message}");
                    _logger.LogError("Failed to save image #{Index}: {Message}", i + 1, result.Message);
                }
                else
                {
                    savedImages.Add(result.Data);
                }
            }

            if (errors.Any())
            {
                var warningMessage = $"Some images failed to save: {string.Join(" | ", errors)}";
                _logger.LogWarning(warningMessage);

                return new Result<List<Image>>
                {
                    Message = savedImages.Any() ? "Partial success" : "All images failed",
                    Success = savedImages.Any(),
                    Warnings = errors,
                    Data = savedImages,
                    StatusCode = savedImages.Any() ? 207 : 400
                };
            }

            return Result<List<Image>>.Ok(savedImages, $"Successfully saved {savedImages.Count} images");
        }

        // Public Wrappers - Single Images
        public Task<Result<Image>> SaveCustomerImageAsync(IFormFile image, string userId)
            => SaveImageAsync(image, "CustomerPhotos", 0, false, userId);

        public Task<Result<Image>> SaveCategoryImageAsync(IFormFile image, int id, string userId)
            => SaveImageAsync(image, "CategoryPhotos", id, false, userId);

        public Task<Result<Image>> SaveCollectionImageAsync(IFormFile image, int id, string userId)
            => SaveImageAsync(image, "CollectionPhotos", id, false, userId);

        public Task<Result<Image>> SaveProductImageAsync(IFormFile image, int id, string userId)
            => SaveImageAsync(image, "ProductPhotos", id, false, userId);

        public Task<Result<Image>> SaveSubCategoryImageAsync(IFormFile image, int id, string userId)
            => SaveImageAsync(image, "SubCategoryPhotos", id, false, userId);

        // Public Wrappers - Main Images
        public Task<Result<Image>> SaveMainCategoryImageAsync(IFormFile image, int id, string? userId = null)
            => SaveImageAsync(image, "CategoryPhotos", id, true, userId);

        public Task<Result<Image>> SaveMainCollectionImageAsync(IFormFile image, int id, string? userId = null)
            => SaveImageAsync(image, "CollectionPhotos", id, true, userId);

        public Task<Result<Image>> SaveMainProductImageAsync(IFormFile image, int id, string? userId = null)
            => SaveImageAsync(image, "ProductPhotos", id, true, userId);

        public Task<Result<Image>> SaveMainSubCategoryImageAsync(IFormFile image, int id, string? userId = null)
            => SaveImageAsync(image, "SubCategoryPhotos", id, true, userId);

        // Public Wrappers - Multiple Images
        public Task<Result<List<Image>>> SaveCategoryImagesAsync(List<IFormFile> images, int id, string userId)
            => SaveImagesAsync(images, id, "CategoryPhotos", userId);

        public Task<Result<List<Image>>> SaveCollectionImagesAsync(List<IFormFile> images, int id, string userId)
            => SaveImagesAsync(images, id, "CollectionPhotos", userId);

        public Task<Result<List<Image>>> SaveProductImagesAsync(List<IFormFile> images, int id, string userId)
            => SaveImagesAsync(images, id, "ProductPhotos", userId);

        public Task<Result<List<Image>>> SaveSubCategoryImagesAsync(List<IFormFile> images, int id, string userId)
            => SaveImagesAsync(images, id, "SubCategoryPhotos", userId);

        // Delete Methods
        public async Task<Result<string>> DeleteImageAsync(int imageId)
        {
            var image = await _unitOfWork.Image.GetByIdAsync(imageId);
            if (image == null)
            {
                _logger.LogWarning("Image not found with ID: {ImageId}", imageId);
                return Result<string>.Fail($"Image not found with ID: {imageId}", 404);
            }
            return await DeleteImageAsync(image);
        }

        public async Task<Result<string>> DeleteImageAsync(Image image)
        {
            _logger.LogInformation("Deleting image ID: {ImageId}", image.Id);

            try
            {
                // Delete from database first
                _unitOfWork.Image.Remove(image);
                await _unitOfWork.CommitAsync();

                // Enqueue ImageKit deletion in background if FileId exists
                if (!string.IsNullOrEmpty(image.CloudinaryPublicId))
                {
                    _backgroundJobClient.Enqueue(() => DeleteFromImageKitAsync(image.CloudinaryPublicId));
                }

                _logger.LogInformation("Image {ImageId} deleted from database", image.Id);
                return Result<string>.Ok("Image deleted successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting image {ImageId}", image.Id);
                NotifyAdminOfError($"Error deleting image {image.Id}: {ex.Message}", ex.StackTrace);
                return Result<string>.Fail("An error occurred while deleting the image", 500);
            }
        }

        public async Task<Result<List<string>>> DeleteImagesAsync(List<Image> images)
        {
            if (images == null || !images.Any())
                return Result<List<string>>.Fail("No images provided for deletion");

            _logger.LogInformation("Deleting {Count} images", images.Count);

            var deletedIds = new List<string>();
            var fileIdsToDelete = new List<string>();

            try
            {
                // Delete from database first (atomic operation)
                _unitOfWork.Image.RemoveList(images);
                await _unitOfWork.CommitAsync();

                _logger.LogInformation("All {Count} images removed from database", images.Count);

                foreach (var image in images)
                {
                    if (!string.IsNullOrEmpty(image.CloudinaryPublicId))
                    {
                        fileIdsToDelete.Add(image.CloudinaryPublicId);
                    }
                    deletedIds.Add(image.Id.ToString());
                }

                // Enqueue ImageKit bulk deletion in background
                if (fileIdsToDelete.Count > 0)
                {
                    _backgroundJobClient.Enqueue(() => DeleteManyFromImageKitAsync(fileIdsToDelete));
                }

                return Result<List<string>>.Ok(deletedIds, "Images deleted successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting images");
                NotifyAdminOfError($"Error deleting images: {ex.Message}", ex.StackTrace);
                return Result<List<string>>.Fail("Error occurred while deleting images", 500);
            }
        }

        // Background job methods for ImageKit deletion
        public async Task DeleteFromImageKitAsync(string fileId)
        {
            try
            {
                await _imageStorageService.DeleteAsync(fileId, CancellationToken.None);
                _logger.LogInformation("Successfully deleted from ImageKit: {FileId}", fileId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to delete from ImageKit: {FileId}", fileId);
                NotifyAdminOfError($"ImageKit deletion failed for {fileId}: {ex.Message}", ex.StackTrace);
            }
        }

        public async Task DeleteManyFromImageKitAsync(List<string> fileIds)
        {
            try
            {
                await _imageStorageService.DeleteManyAsync(fileIds, CancellationToken.None);
                _logger.LogInformation("Successfully bulk deleted {Count} files from ImageKit", fileIds.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to bulk delete from ImageKit: {Message}", ex.Message);
                NotifyAdminOfError($"ImageKit bulk deletion failed: {ex.Message}", ex.StackTrace);
            }
        }
    }
}
