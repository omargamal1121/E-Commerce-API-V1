using DomainLayer.Enums;
using ApplicationLayer.DtoModels.ImagesDtos;
using ApplicationLayer.Interfaces;
using ApplicationLayer.Services.AdminOperationServices;

using ApplicationLayer.Services.EmailServices;
using ApplicationLayer.Services.SubCategoryServices;
using Hangfire;


using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Http;
using ApplicationLayer.Services.CollectionServices;

namespace ApplicationLayer.Services.ProductServices
{
	public interface IProductImageService
	{

		Task<Result<List<ImageDto>>> GetProductImagesAsync(int productId);
		Task<Result<List<ImageDto>>> AddProductImagesAsync(int productId, List<IFormFile> images, string userId);
		Task<Result<bool>> RemoveProductImageAsync(int productId, int imageId, string userId);
		Task<Result<ImageDto>> UploadAndSetMainImageAsync(int productId, Microsoft.AspNetCore.Http.IFormFile mainImage, string userId);
		
	}

	public class ProductImageService : IProductImageService
	{
		private readonly IBackgroundJobClient _backgroundJobClient;
		private readonly IUnitOfWork _unitOfWork;
		private readonly ILogger<ProductImageService> _logger;
		private readonly IAdminOpreationServices _adminOpreationServices;
		private readonly IErrorNotificationService _errorNotificationService;
		private readonly IImagesServices _imagesServices;
		private readonly ISubCategoryServices _subCategoryServices;
		private readonly IProductCatalogService _productCatalogService;
		private readonly IProductCacheManger _productCacheManger;
		private readonly ICollectionCacheHelper _collectionCacheHelper;
		private readonly ISubCategoryCacheHelper _subCategoryCacheHelper;

		public ProductImageService(
			ISubCategoryCacheHelper subCategoryCache,
			IProductCacheManger productCache ,
			ICollectionCacheHelper collectionCache,
			ISubCategoryCacheHelper subCategoryCacheHelper,
			IProductCacheManger productCacheManger,
			ICollectionCacheHelper collectionCacheHelper,
			IBackgroundJobClient backgroundJobClient,
			IUnitOfWork unitOfWork,
			ILogger<ProductImageService> logger,
			IAdminOpreationServices adminOpreationServices,
			IErrorNotificationService errorNotificationService,
			IImagesServices imagesServices,
			ISubCategoryServices subCategoryServices,
			IProductCatalogService productCatalogService)
		{
			_subCategoryCacheHelper = subCategoryCache;
			_productCacheManger = productCacheManger;
			_backgroundJobClient = backgroundJobClient;
			_unitOfWork = unitOfWork;
			_logger = logger;
			_adminOpreationServices = adminOpreationServices;
			_errorNotificationService = errorNotificationService;
			_imagesServices = imagesServices;
			_subCategoryServices = subCategoryServices;
			_productCatalogService = productCatalogService;
			_collectionCacheHelper = collectionCacheHelper;
		}
		private void RemoveProductCaches()
		{
			_collectionCacheHelper.ClearCollectionDataCache();
			_subCategoryCacheHelper.ClearSubCategoryDataCache();
			_productCacheManger.ClearProductCache();

		}

	
		public async Task<Result<List<ImageDto>>> GetProductImagesAsync(int productId)
		{
			try
			{

				if(!await _unitOfWork.Product.IsExsistAsync(productId))
				{
					_logger.LogWarning("GetProductImagesAsync: Product not found. productId: {ProductId}", productId);
					return Result<List<ImageDto>>.Fail($"No Product with this id :{productId}");
				}
				var images = await _unitOfWork.Image.GetAll()
					.Where(i => i.ProductId == productId && i.DeletedAt == null)
					.Select(i => new ImageDto
					{
						Id = i.Id,
						Url = i.Url,
						IsMain = i.IsMain
					})
					.ToListAsync();

				if (!images.Any())
				{
					_logger.LogWarning("GetProductImagesAsync: No images found for productId: {ProductId}", productId);
					return Result<List<ImageDto>>.Fail("No images found for this product", 404);
				}

				_logger.LogInformation("GetProductImagesAsync: Successfully retrieved {Count} images for productId: {ProductId}", images.Count, productId);
				return Result<List<ImageDto>>.Ok(images, "Product images retrieved successfully", 200);
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, $"Error in GetProductImagesAsync for productId: {productId}");
				 	_backgroundJobClient.Enqueue(()=> _errorNotificationService.SendErrorNotificationAsync(ex.Message, ex.StackTrace));
				return Result<List<ImageDto>>.Fail("Error retrieving product images", 500);
			}
		}

		public async Task<Result<List<ImageDto>>> AddProductImagesAsync(int productId, List<IFormFile> images, string userId)
		{
			_logger.LogInformation($"Adding images to product: {productId}");
			
			if (images == null || !images.Any())
				{
					_logger.LogWarning("AddProductImagesAsync: No images provided for productId: {ProductId}", productId);
					return Result<List<ImageDto>>.Fail("No images provided", 400);
				}
			
			var product = await _unitOfWork.Product.IsExsistAsync(productId);
			if (!product)
				{
					_logger.LogWarning("AddProductImagesAsync: Product not found. productId: {ProductId}", productId);
					return Result<List<ImageDto>>.Fail("Product not found", 404);
				}
			
			using var transaction = await _unitOfWork.BeginTransactionAsync();
			try
			{
				var saveResult = await _imagesServices.SaveProductImagesAsync(images,productId, userId);
				if (!saveResult.Success || saveResult.Data == null)
				{
					_logger.LogWarning("AddProductImagesAsync: Failed to save images for productId: {ProductId}", productId);
					await transaction.RollbackAsync();
					return Result<List<ImageDto>>.Fail("Failed to save images", 500, saveResult.Warnings);
				}

			

			

				var addedImages = saveResult.Data.Select(i => new ImageDto
				{
					Id = i.Id,
					Url = i.Url,
					IsMain = i.IsMain
				}).ToList();

				var adminOpResult = await _adminOpreationServices.AddAdminOpreationAsync(
					$"Add Images to Product {productId}",
					Opreations.AddOpreation,
					userId,
					productId
				);
				if (adminOpResult == null || !adminOpResult.Success)
				{
					_logger.LogError("Failed to log admin operation for AddProductImagesAsync on productId: {ProductId}, userId: {UserId}. Rolling back transaction.", productId, userId);
					await transaction.RollbackAsync();
					return Result<List<ImageDto>>.Fail("Failed to log admin operation. Image addition rolled back.", 500);
				}

				await _unitOfWork.CommitAsync();
				await transaction.CommitAsync();
				RemoveProductCaches();



				_logger.LogInformation($"Successfully added {addedImages.Count} images to product {productId}");

				return Result<List<ImageDto>>.Ok(addedImages, "Images added successfully", 201, saveResult.Warnings);
			}
			catch (Exception ex)
			{
				await transaction.RollbackAsync();
				_logger.LogError(ex, $"Error in AddProductImageAsync for productId: {productId}");
				 	_backgroundJobClient.Enqueue(()=> _errorNotificationService.SendErrorNotificationAsync(ex.Message, ex.StackTrace));
				return Result<List<ImageDto>>.Fail("Error adding images", 500);
			}
		}

		public async Task<Result<bool>> RemoveProductImageAsync(int productId, int imageId, string userId)
		{
			_logger.LogInformation($"Removing image {imageId} from product: {productId}");

			using var transaction = await _unitOfWork.BeginTransactionAsync();
			try
			{
				var productinfo = _unitOfWork.Product.GetAll().Where(p=>p.Id==productId).Select(p => new
				{
					image=p.Images.FirstOrDefault(i=>i.Id==imageId),
					imagecount=p.Images.Count,
					isactive= p.IsActive,
					isdeleted=p.DeletedAt==null,
					subcategoryid=p.SubCategoryId,
					hasvaraint=p.ProductVariants.Any(v=>v.DeletedAt==null&&v.Quantity>0)
				}).FirstOrDefault();
			
				if (productinfo==null||productinfo.isdeleted)
				{
					_logger.LogWarning("RemoveProductImageAsync: Product not found or deleted. productId: {ProductId}", productId);
					return Result<bool>.Fail("Product not found", 404);
				}
				if(productinfo.image==null)
				{
					_logger.LogWarning("RemoveProductImageAsync: Image not found. productId: {ProductId}, imageId: {ImageId}", productId, imageId);
					return Result<bool>.Fail("Image not found", 404);

				}

				var isdeleted = await _imagesServices.DeleteImageAsync(imageId);
			
				if (!isdeleted.Success )
				{
					_logger.LogWarning("RemoveProductImageAsync: Image not found or already deleted. productId: {ProductId}, imageId: {ImageId}", productId, imageId);
					await transaction.RollbackAsync();
					return Result<bool>.Fail(isdeleted.Message, isdeleted.StatusCode);
				}

				

			
			
				var warnings = new List<string>();

				if (productinfo.isactive&&productinfo.imagecount<=1)
				{
					if( productinfo.hasvaraint)
					{
						_logger.LogWarning("RemoveProductImageAsync: Attempted to remove last image from active product with variants. productId: {ProductId}", productId);
						await transaction.RollbackAsync();
						return Result<bool>.Fail("Can't Remove all images of active product");
					}
					await _productCatalogService.DeactivateProductAsync(productId, userId);
					_backgroundJobClient.Enqueue(()=> _subCategoryServices.DeactivateSubCategoryIfAllProductsAreInactiveAsync(productinfo.subcategoryid, userId));

					warnings.Add("Product was deactivated because its last image was removed.");
				}

			 var isadded=	await _adminOpreationServices.AddAdminOpreationAsync(
					$"Remove Image {imageId} from Product {productId}",
					Opreations.DeleteOpreation,
					userId,
					productId
				);
				if(isadded==null)
				{
					_logger.LogError("Failed to log admin operation for RemoveProductImageAsync on productId: {ProductId}, imageId: {ImageId}, userId: {UserId}. Rolling back transaction.", productId, imageId, userId);
					await transaction.RollbackAsync();
					return Result<bool>.Fail("Unexpected error while removing image", 500);
				}	

				await _unitOfWork.CommitAsync();
				await transaction.CommitAsync();

				

				RemoveProductCaches();

				_logger.LogInformation($"Image {imageId} removed from product {productId}");

				return Result<bool>.Ok(true, "Image removed", 200, warnings: warnings);
			}
			catch (Exception ex)
			{
				await transaction.RollbackAsync();
				_logger.LogError(ex, $"Error in RemoveProductImageAsync for productId: {productId}, imageId: {imageId}");
				 	_backgroundJobClient.Enqueue(()=> _errorNotificationService.SendErrorNotificationAsync(ex.Message, ex.StackTrace));
				return Result<bool>.Fail("Unexpected error while removing image", 500);
			}
		}


		public async Task<Result<ImageDto>> UploadAndSetMainImageAsync(int productId, IFormFile mainImage, string userId)
		{
			_logger.LogInformation($"Setting new main image for product: {productId}");
			
			var product = await _unitOfWork.Product.IsExsistAsync(productId);
			if (!product)
				{
					_logger.LogWarning("UploadAndSetMainImageAsync: Product not found. productId: {ProductId}", productId);
					return Result<ImageDto>.Fail("Product not found", 404);
				}
			
			using var transaction = await _unitOfWork.BeginTransactionAsync();
			try
			{
				
				// Save new image
				var saveResult = await _imagesServices.SaveMainProductImageAsync(mainImage,productId, userId);
				if (!saveResult.Success || saveResult.Data == null)
				{
					_logger.LogWarning("UploadAndSetMainImageAsync: Failed to save main image for productId: {ProductId}", productId);
					await transaction.RollbackAsync();
					var errorMsg = !string.IsNullOrWhiteSpace(saveResult.Message)
						? saveResult.Message
						: "Image saving failed. No image returned.";
					return Result<ImageDto>.Fail(errorMsg, 400);
				}

				

			var isadded =await _adminOpreationServices.AddAdminOpreationAsync(
					$"Set Main Image for Product {productId}",
					Opreations.UpdateOpreation,
					userId,
					productId
				);
				if (isadded == null)
				{
					_logger.LogError("Failed to log admin operation for UploadAndSetMainImageAsync on productId: {ProductId}, userId: {UserId}. Rolling back transaction.", productId, userId);
					await transaction.RollbackAsync();
					return Result<ImageDto>.Fail("Unexpected error while removing image", 500);
				}

				await _unitOfWork.CommitAsync();
				await transaction.CommitAsync();
				_logger.LogInformation($"Main image set successfully for product {productId}");
				var imageDto = new ImageDto
				{
					Id = saveResult.Data.Id,
					Url = saveResult.Data.Url,
					IsMain = saveResult.Data.IsMain
				};
				RemoveProductCaches();
				return Result<ImageDto>.Ok(imageDto, "Main image updated", 200);
			}
			catch (Exception ex)
			{
				await transaction.RollbackAsync();
				_logger.LogError(ex, $"Error in AddMainImageAsync for productId: {productId}");
				 	_backgroundJobClient.Enqueue(()=> _errorNotificationService.SendErrorNotificationAsync(ex.Message, ex.StackTrace));
				return Result<ImageDto>.Fail("Error setting main image", 500);
			}
		}

	
	}
} 

