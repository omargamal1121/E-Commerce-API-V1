using CloudinaryDotNet;
using CloudinaryDotNet.Actions;
using E_Commerce.Enums;
using E_Commerce.Interfaces;
using E_Commerce.Models;
using E_Commerce.Services.AdminOperationServices;
using E_Commerce.Services.EmailServices;
using E_Commerce.UOW;
using Hangfire;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace E_Commerce.Services
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
		private string[] AllowedContentTypes => _configuration.GetSection("Security:FileUpload:AllowedContentTypes").Get<string[]>() ?? new[] { "image/jpeg", "image/jpg", "image/png", "image/gif", "image/webp" };
		private string[] AllowedExtensions => _configuration.GetSection("Security:FileUpload:AllowedExtensions").Get<string[]>() ?? new[] { ".jpg", ".jpeg", ".png", ".gif", ".webp" };
		private readonly byte[][] _fileSignatures = {
			new byte[] { 0xFF, 0xD8, 0xFF },
			new byte[] { 0x89, 0x50, 0x4E, 0x47 },
			new byte[] { 0x47, 0x49, 0x46, 0x38 },
			new byte[] { 0x52, 0x49, 0x46, 0x46 },
		};

		public ImagesServices(IErrorNotificationService errorNotificationService ,IBackgroundJobClient backgroundJobClient ,Cloudinary cloudinary ,IHttpContextAccessor httpContextAccessor,ILogger<ImagesServices> logger, IConfiguration configuration, IUnitOfWork unitOfWork, IAdminOpreationServices adminOpreationServices)
		{
			_errorNotificationService = errorNotificationService;
			_backgroundJobClient = backgroundJobClient;
			_cloudinary = cloudinary;
			_httpContextAccessor= httpContextAccessor;
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
			
			catch { return false; }
		}

		public bool IsValidFileSize(long fileSize) => fileSize > 0 && fileSize <= MaxFileSize;

		public string GetFolderPath(params string[] folders)
		{
			string basePath = Directory.GetCurrentDirectory();
			string folderPath = folders.Aggregate(basePath, Path.Combine);
			if (!Directory.Exists(folderPath)) Directory.CreateDirectory(folderPath);
			return folderPath;
		}

		private async Task LogAdminOperationAsync(string userId, string description, int itemId)
		{
			await _adminOpreationServices.AddAdminOpreationAsync(description, Opreations.AddOpreation, userId, itemId);
		}

		private void NotifyAdminOfError(string message, string? stackTrace = null)
		{
			BackgroundJob.Enqueue<IErrorNotificationService>(_ => _.SendErrorNotificationAsync(message, stackTrace));
		}

		private async Task<Result<Image>> SaveImageAsync(IFormFile image, string folderName, int id, string? userId = null)
		{
			_logger.LogInformation($"📥 Saving image to {folderName}");
			if (image is null) return Result<Image>.Fail("Image is null");

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

				if (!IsValidFileSignature(stream))
					return Result<Image>.Fail("Invalid file format detected");

				stream.Position = 0;

				var uploadParams = new ImageUploadParams
				{
					File = new FileDescription(image.FileName, stream),
					Folder = folderName,
					PublicId = Guid.NewGuid().ToString(),
					Overwrite = false,
				};

				var uploadResult = await _cloudinary.UploadAsync(uploadParams);

				if (uploadResult == null || string.IsNullOrEmpty(uploadResult.SecureUrl?.ToString()))
					return Result<Image>.Fail("Failed to upload image to Cloudinary");


				//string folderPath = GetFolderPath("wwwroot", folderName);
				//string uniqueName = $"{Guid.NewGuid()}{extension}";
				//string filePath = Path.Combine(folderPath, uniqueName);

				//using var fileStream = new FileStream(filePath, FileMode.Create);
				//await stream.CopyToAsync(fileStream);
				//var request = _httpContextAccessor.HttpContext?.Request;

				//string relativePath = $"/{folderName}/{uniqueName}";
				//string baseUrl = $"{request?.Scheme}://{request?.Host}";
				//string fullUrl = $"{baseUrl}/{folderName}/{uniqueName}";



				var fullUrl = uploadResult.SecureUrl.ToString();

				var savedImage = new Image
				{
					UploadDate = DateTime.Now,
					Folder = folderName,
					Url = fullUrl,
					FileSize = image.Length,
					FileType = image.ContentType,
				};

				switch (folderName.ToLower())
				{
					case "categoryphotos":
						savedImage.CategoryId = id;
						break;
					case "productphotos":
						savedImage.ProductId = id;
						break;
					case "subcategoryphotos":
						savedImage.SubCategoryId = id;
						break;
					case "collectionphotos":
						savedImage.CollectionId = id;
						break;
					case "customerphotos":
						savedImage.CustomerId = userId;
						break;
				}

				var imageRepo = _unitOfWork.Repository<Image>();
				await imageRepo.CreateAsync(savedImage);
				await _unitOfWork.CommitAsync();

				if (!string.IsNullOrEmpty(userId))
					await LogAdminOperationAsync(userId, $"Uploaded image to {folderName}", savedImage.Id);

				_logger.LogInformation($"✅ Image saved: {fullUrl}");
				return Result<Image>.Ok(savedImage);
			}
			catch (Exception ex)
			{
				_logger.LogError($"❌ Error saving image: {ex.Message}");
				NotifyAdminOfError(ex.Message, ex.StackTrace);
				return Result<Image>.Fail($"Error saving image: {ex.Message}",500);
			}
		}


		private async Task<Result<Image>> SaveMainImageAsync(IFormFile image, string folderName, int id, string? userId = null)
		{
			_logger.LogInformation($"📥 Saving main image to {folderName}");
			if (image is null) return Result<Image>.Fail("Image is null");

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
				if (!IsValidFileSignature(stream))
					return Result<Image>.Fail("Invalid file format detected");
				stream.Position = 0;

				var uploadParams = new ImageUploadParams
				{
					File = new FileDescription(image.FileName, stream),
					Folder = folderName,
					PublicId = Guid.NewGuid().ToString(),
					Overwrite = false,
				};

				var uploadResult = await _cloudinary.UploadAsync(uploadParams);

				if (uploadResult == null || string.IsNullOrEmpty(uploadResult.SecureUrl?.ToString()))
					return Result<Image>.Fail("Failed to upload image to Cloudinary");

				var fullUrl = uploadResult.SecureUrl.ToString();

				var savedImage = new Image
				{
					UploadDate = DateTime.Now,
					Folder = folderName,
					Url = fullUrl,
					FileSize = image.Length,
					FileType = image.ContentType,
					IsMain = true
				};

				switch (folderName.ToLower())
				{
					case "categoryphotos":
						savedImage.CategoryId = id;
						break;
					case "productphotos":
						savedImage.ProductId = id;
						break;
					case "subcategoryphotos":
						savedImage.SubCategoryId = id;
						break;
					case "collectionphotos":
						savedImage.CollectionId = id;
						break;
					case "customerphotos":
						savedImage.CustomerId = userId;
						break;
				}

				var imageRepo = _unitOfWork.Repository<Image>();
				await imageRepo.CreateAsync(savedImage);
				await _unitOfWork.CommitAsync();

				if (!string.IsNullOrEmpty(userId))
					await LogAdminOperationAsync(userId, $"Uploaded main image to {folderName}", savedImage.Id);

				_logger.LogInformation($"✅ Main image saved: {fullUrl}");
				return Result<Image>.Ok(savedImage);
			}
			catch (Exception ex)
			{
				_logger.LogError($"❌ Error saving main image: {ex.Message}");
				NotifyAdminOfError(ex.Message, ex.StackTrace);
				return Result<Image>.Fail($"Error saving main image: {ex.Message}");
			}
		}


		private async Task<Result<List<Image>>> SaveImagesAsync(List<IFormFile> images,int id, string folderName, string userId)
		{
			_logger.LogInformation($"📥 Saving {images?.Count} images to {folderName}");
			if (images == null || images.Count == 0) return Result<List<Image>>.Fail("Images are null or empty");

			var savedImages = new List<Image>();
			var errors = new List<string>();
			int counter = 1;

			foreach (var image in images)
			{
				var result = await SaveImageAsync(image, folderName,id, userId);
				if (!result.Success || result.Data == null)
				{
					errors.Add($"Image #{counter}: {result.Message}");
					_logger.LogError($"❌ Failed to save image #{counter}: {result.Message}");
				}
				else
				{
					savedImages.Add(result.Data);
				}
				counter++;
			}

			// Return success with warnings if some images failed
			if (errors.Any())
			{
				var warningMessage = $"Some images failed to save: {string.Join(" | ", errors)}";
				_logger.LogWarning(warningMessage);
				
				return new Result<List<Image>>
				{
					Message="Error",
					Success = true, 
					Warnings = errors,
					Data = savedImages
				};
			}
			return savedImages.Count==0?Result<List<Image>>.Fail("No Images Saved",400,errors):Result<List<Image>>.Ok(savedImages);
		}

		// Public Wrappers
		public Task<Result<Image>> SaveCustomerImageAsync(IFormFile image, string userId) => SaveImageAsync(image, "CustomerPhotos", 0,userId);
		
		// Single image methods
		public Task<Result<Image>> SaveCategoryImageAsync(IFormFile image, int id, string userId) => SaveImageAsync(image, "CategoryPhotos", id, userId);
		public Task<Result<Image>> SaveCollectionImageAsync(IFormFile image, int id, string userId) => SaveImageAsync(image, "CollectionPhotos", id, userId);
		public Task<Result<Image>> SaveProductImageAsync(IFormFile image, int id, string userId) => SaveImageAsync(image, "ProductPhotos", id, userId);
		public Task<Result<Image>> SaveSubCategoryImageAsync(IFormFile image, int id, string userId) => SaveImageAsync(image, "SubCategoryPhotos", id, userId);
		
		// Multiple images methods
		public Task<Result<List<Image>>> SaveCategoryImagesAsync(List<IFormFile> images,int id, string userId) => SaveImagesAsync(images, id, "CategoryPhotos", userId);
		public Task<Result<List<Image>>> SaveCollectionImagesAsync(List<IFormFile> images, int id, string userId) => SaveImagesAsync(images,  id, "CollectionPhotos", userId);
		public Task<Result<List<Image>>> SaveProductImagesAsync(List<IFormFile> images, int id, string userId) => SaveImagesAsync(images,  id, "ProductPhotos", userId);
		public Task<Result<List<Image>>> SaveSubCategoryImagesAsync(List<IFormFile> images, int id, string userId) => SaveImagesAsync(images,  id, "SubCategoryPhotos", userId);
		
		public Task<Result<Image>> SaveMainCategoryImageAsync(IFormFile image, int id, string? userId = null) => SaveMainImageAsync(image, "CategoryPhotos", id, userId);
		public Task<Result<Image>> SaveMainCollectionImageAsync(IFormFile image, int id, string? userId = null) => SaveMainImageAsync(image, "CollectionPhotos", id, userId);
		public Task<Result<Image>> SaveMainProductImageAsync(IFormFile image, int id, string? userId = null) => SaveMainImageAsync(image, "ProductPhotos", id, userId);
		public Task<Result<Image>> SaveMainSubCategoryImageAsync(IFormFile image, int id, string? userId = null) => SaveMainImageAsync(image, "SubCategoryPhotos", id, userId);
		public async Task<Result<string>> DeleteImageAsync(int imageid)
		{
			var image= await _unitOfWork.Image.GetByIdAsync(imageid);
			if (image == null){
				_logger.LogInformation($"Image not found by this id:{imageid}");
				return Result<string>.Fail($"Image not found by this id:{imageid}",404);
			}
			return await DeleteImageAsync(image);

        }
		public async Task<Result<string>> DeleteImageAsync(Image image)
		{
			_logger.LogInformation("✅ Execute {Method} for image ID: {ImageId}", nameof(DeleteImageAsync), image.Id);

			try
			{
				// Extract filename from URL (Cloudinary public_id usually without extension)
				var uri = new Uri(image.Url);
				var filename = Path.GetFileNameWithoutExtension(uri.AbsolutePath);

				var deletionParams = new DeletionParams(filename);
				var deletionResult = await _cloudinary.DestroyAsync(deletionParams);

				if (deletionResult.Result != "ok" && deletionResult.Result != "not found")
				{
					_logger.LogWarning("⚠️ Failed to delete image {ImageId} from Cloudinary: {Result}", image.Id, deletionResult.Result);
					return Result<string>.Fail($"Failed to delete image from Cloudinary: {deletionResult.Result}");
				}

				_unitOfWork.Image.Remove(image);
				await _unitOfWork.CommitAsync();

				_logger.LogInformation("🗑️ Image ID {ImageId} deleted from Cloudinary and marked deleted in DB", image.Id);
				return Result<string>.Ok("Image deleted successfully");
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "❌ Error deleting image {ImageId}", image.Id);
				NotifyAdminOfError(ex.Message, ex.StackTrace);
				return Result<string>.Fail("An error occurred while deleting the image");
			}
		}

		public async Task<Result<List<string>>> DeleteImagesAsync(List<Models.Image> images)
		{
			var errorMessages = new List<string>();
			var successfullyDeleted = new List<Models.Image>();

			foreach (var image in images)
			{
				try
				{
					var uri = new Uri(image.Url);
					var filename = Path.GetFileNameWithoutExtension(uri.AbsolutePath);

					var deletionParams = new DeletionParams(filename);
					var deletionResult = await _cloudinary.DestroyAsync(deletionParams);

					if (deletionResult.Result == "ok" || deletionResult.Result == "not found")
					{
						successfullyDeleted.Add(image);
						_logger.LogInformation("🗑️ Image {ImageId} deleted from Cloudinary successfully", image.Id);
					}
					else
					{
						_logger.LogWarning("⚠️ Failed to delete image {ImageId} from Cloudinary: {Result}", image.Id, deletionResult.Result);
						errorMessages.Add($"Image ID {image.Id}: {deletionResult.Result}");
					}
				}
				catch (Exception ex)
				{
					_logger.LogError(ex, "❌ Error deleting image {ImageId} from Cloudinary", image.Id);
					_backgroundJobClient.Enqueue(() => _errorNotificationService.SendErrorNotificationAsync(ex.Message, ex.StackTrace));
					errorMessages.Add($"Image ID {image.Id}: Exception occurred");
				}
			}

			if (successfullyDeleted.Any())
			{
				_unitOfWork.Image.RemoveList(successfullyDeleted);
				await _unitOfWork.CommitAsync();
				_logger.LogInformation("✅ {Count} images removed from database", successfullyDeleted.Count);
			}

			if (errorMessages.Count == 0)
				return Result<List<string>>.Ok(new List<string> { "✅ All images deleted successfully" });

			return Result<List<string>>.Fail("Some images could not be deleted", errorMessages);
		}


	}
}
