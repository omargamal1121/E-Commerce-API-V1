using ApplicationLayer.Interfaces;
using ApplicationLayer.Services.AdminOperationServices;
using ApplicationLayer.Services.EmailServices;
using CloudinaryDotNet;
using CloudinaryDotNet.Actions;
using DomainLayer.Enums;
using DomainLayer.Models;
using Hangfire;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace ApplicationLayer.Services
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
        private readonly Cloudinary _cloudinary;

        private int MaxFileSize => _configuration.GetValue<int>("Security:FileUpload:MaxFileSizeMB", 5) * 1024 * 1024;
        private string[] AllowedContentTypes => _configuration.GetSection("Security:FileUpload:AllowedContentTypes").Get<string[]>()
            ?? new[] { "image/jpeg", "image/jpg", "image/png", "image/gif", "image/webp" };
        private string[] AllowedExtensions => _configuration.GetSection("Security:FileUpload:AllowedExtensions").Get<string[]>()
            ?? new[] { ".jpg", ".jpeg", ".png", ".gif", ".webp" };

        private readonly byte[][] _fileSignatures = {
            new byte[] { 0xFF, 0xD8, 0xFF },      // JPEG
            new byte[] { 0x89, 0x50, 0x4E, 0x47 }, // PNG
            new byte[] { 0x47, 0x49, 0x46, 0x38 }, // GIF
            new byte[] { 0x52, 0x49, 0x46, 0x46 }, // WEBP
        };

        public ImagesServices(
            IErrorNotificationService errorNotificationService,
            IBackgroundJobClient backgroundJobClient,
            Cloudinary cloudinary,
            IHttpContextAccessor httpContextAccessor,
            ILogger<ImagesServices> logger,
            IConfiguration configuration,
            IUnitOfWork unitOfWork,
            IAdminOpreationServices adminOpreationServices)
        {
            _errorNotificationService = errorNotificationService;
            _backgroundJobClient = backgroundJobClient;
            _cloudinary = cloudinary;
            _httpContextAccessor = httpContextAccessor;
            _logger = logger;
            _configuration = configuration;
            _unitOfWork = unitOfWork;
            _adminOpreationServices = adminOpreationServices;
        }

        public bool IsValidExtension(string extension) => AllowedExtensions.Contains(extension.ToLower());
        public bool IsValidContentType(string contentType) => AllowedContentTypes.Contains(contentType.ToLower());

        public bool IsValidFileSignature(Stream fileStream)
        {
            try
            {
                using var reader = new BinaryReader(fileStream, System.Text.Encoding.UTF8, true);
                var headerBytes = reader.ReadBytes(8);
                return _fileSignatures.Any(sig => headerBytes.Take(sig.Length).SequenceEqual(sig));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating file signature");
                return false;
            }
        }

        public bool IsValidFileSize(long fileSize) => fileSize > 0 && fileSize <= MaxFileSize;

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

            if (image == null)
                return Result<Image>.Fail("Image is null");

            // Validate file
            if (!IsValidFileSize(image.Length))
                return Result<Image>.Fail($"File size must be between 1 and {MaxFileSize / (1024 * 1024)}MB");

            if (!IsValidContentType(image.ContentType))
                return Result<Image>.Fail($"Invalid content type: {image.ContentType}");

            string extension = Path.GetExtension(image.FileName);
            if (!IsValidExtension(extension))
                return Result<Image>.Fail($"Invalid extension. Allowed: {string.Join(", ", AllowedExtensions)}");

            try
            {
                using var stream = image.OpenReadStream();

                // Validate file signature
                if (!IsValidFileSignature(stream))
                    return Result<Image>.Fail("Invalid file format detected");

                stream.Position = 0;

                // Upload to Cloudinary
                var publicId = Guid.NewGuid().ToString();
                var uploadParams = new ImageUploadParams
                {
                    File = new FileDescription(image.FileName, stream),
                    Folder = folderName,
                    PublicId = publicId,
                    Overwrite = false,
                };

                var uploadResult = await _cloudinary.UploadAsync(uploadParams);

                if (uploadResult?.SecureUrl == null || uploadResult.StatusCode != System.Net.HttpStatusCode.OK)
                {
                    _logger.LogError("Cloudinary upload failed: {Error}", uploadResult?.Error?.Message);
                    return Result<Image>.Fail("Failed to upload image to Cloudinary");
                }

                // Create image entity
                var savedImage = new Image
                {
                    CloudinaryPublicId = uploadResult.PublicId, // CRITICAL: Store PublicId for deletion
                    UploadDate = DateTime.UtcNow, // Use UTC
                    Folder = folderName,
                    Url = uploadResult.SecureUrl.ToString(),
                    FileSize = image.Length,
                    FileType = image.ContentType,
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
                    StatusCode = savedImages.Any() ? 207 : 400 // 207 = Multi-Status
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

            if (string.IsNullOrEmpty(image.CloudinaryPublicId))
            {
                _logger.LogWarning("Image {ImageId} has no CloudinaryPublicId, skipping Cloudinary deletion", image.Id);
            }

            try
            {
                // Delete from database first
                _unitOfWork.Image.Remove(image);
                await _unitOfWork.CommitAsync();

                // Enqueue Cloudinary deletion in background if PublicId exists
                if (!string.IsNullOrEmpty(image.CloudinaryPublicId))
                {
                    _backgroundJobClient.Enqueue(() => DeleteFromCloudinaryAsync(image.CloudinaryPublicId));
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
            var errors = new List<string>();

            try
            {
                // Delete from database first (atomic operation)
                _unitOfWork.Image.RemoveList(images);
                await _unitOfWork.CommitAsync();

                _logger.LogInformation("All {Count} images removed from database", images.Count);

                // Enqueue Cloudinary deletions in background
                foreach (var image in images)
                {
                    if (!string.IsNullOrEmpty(image.CloudinaryPublicId))
                    {
                        _backgroundJobClient.Enqueue(() => DeleteFromCloudinaryAsync(image.CloudinaryPublicId));
                    }
                    deletedIds.Add(image.Id.ToString());
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

        // Background job method for Cloudinary deletion
        public async Task DeleteFromCloudinaryAsync(string publicId)
        {
            try
            {
                var deletionParams = new DeletionParams(publicId);
                var result = await _cloudinary.DestroyAsync(deletionParams);

                if (result.Result == "ok")
                {
                    _logger.LogInformation("Successfully deleted from Cloudinary: {PublicId}", publicId);
                }
                else
                {
                    _logger.LogWarning("Cloudinary deletion returned: {Result} for {PublicId}", result.Result, publicId);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to delete from Cloudinary: {PublicId}", publicId);
                NotifyAdminOfError($"Cloudinary deletion failed for {publicId}: {ex.Message}", ex.StackTrace);
            }
        }
    }
}