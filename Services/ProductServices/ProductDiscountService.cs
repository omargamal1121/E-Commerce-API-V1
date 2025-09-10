using E_Commerce.DtoModels.DiscoutDtos;
using E_Commerce.DtoModels.ImagesDtos;
using E_Commerce.DtoModels.ProductDtos;
using E_Commerce.DtoModels.Responses;
using E_Commerce.Enums;
using E_Commerce.ErrorHnadling;
using E_Commerce.Interfaces;
using E_Commerce.Models;
using E_Commerce.Services.AdminOperationServices;
using E_Commerce.Services.Cache;
using E_Commerce.Services.CartServices;
using E_Commerce.Services.Collection;
using E_Commerce.Services.EmailServices;
using E_Commerce.Services.Order;
using E_Commerce.Services.SubCategoryServices;
using E_Commerce.UOW;
using Hangfire;
using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;

namespace E_Commerce.Services.ProductServices
{

	public class ProductDiscountService : IProductDiscountService
	{
		private readonly IUnitOfWork _unitOfWork;
		private readonly ILogger<ProductDiscountService> _logger;
		private readonly IProductCacheManger _productCacheManger;
		private readonly IproductMapper _productMapper;
		private readonly IBackgroundJobClient _backgroundJobClient;
		private readonly IAdminOpreationServices _adminOpreationServices;
		private readonly IErrorNotificationService _errorNotificationService;
		private readonly ICartServices _cartServices;
		private readonly ICollectionCacheHelper _collectionCacheHelper ;
	

		private readonly ISubCategoryCacheHelper _SubCategoryCacheHelper;

		public ProductDiscountService(
			ICollectionCacheHelper collectionCacheHelper,
			ISubCategoryCacheHelper subCategoryCacheHelper,
			IProductCacheManger productCacheManger,
			IproductMapper iproductMapper,
			ICartServices	cartServices,
			IBackgroundJobClient backgroundJobClient,
		
			IUnitOfWork unitOfWork,
			ILogger<ProductDiscountService> logger,
			IAdminOpreationServices adminOpreationServices,
			IErrorNotificationService errorNotificationService)
		{ 
			_collectionCacheHelper = collectionCacheHelper;
			_SubCategoryCacheHelper = subCategoryCacheHelper;
			_productCacheManger = productCacheManger;
			_productMapper = iproductMapper;
			_cartServices = cartServices;
			_backgroundJobClient = backgroundJobClient;
			_unitOfWork = unitOfWork;
			_logger = logger;
			_adminOpreationServices = adminOpreationServices;
			_errorNotificationService = errorNotificationService;
		}

		private void RemoveCacheAndRelatedCaches()
		{
			_collectionCacheHelper.ClearCollectionDataCache();
			_SubCategoryCacheHelper.ClearSubCategoryDataCache();
			_productCacheManger.ClearProductCache();
		}

		public async Task<Result<List<ProductDto>>> ApplyDiscountToProductsAsync(ApplyDiscountToProductsDto dto, string userId)
        {
            _logger.LogInformation($"Applying discount {dto?.Discountid} to {dto?.ProductsId?.Count ?? 0} products");
            
            if (dto == null)
                return Result<List<ProductDto>>.Fail("DTO cannot be null.", 400);
            
            if (dto.Discountid <= 0)
                return Result<List<ProductDto>>.Fail("Invalid discount ID.", 400);
            
            if (dto.ProductsId == null || !dto.ProductsId.Any())
                return Result<List<ProductDto>>.Fail("Product IDs cannot be null or empty.", 400);
            
            if (string.IsNullOrWhiteSpace(userId))
                return Result<List<ProductDto>>.Fail("User ID cannot be null or empty.", 400);

            using var transaction = await _unitOfWork.BeginTransactionAsync();
            try
            {
                // Validate discount exists and is not deleted
                var discount = await _unitOfWork.Repository<Models.Discount>().GetByIdAsync(dto.Discountid);
                if (discount == null || discount.DeletedAt != null)
                    return Result<List<ProductDto>>.Fail("Discount not found or deleted.", 404);

                // Check if discount is active and valid
                var now = DateTime.UtcNow;
                var isDiscountActive = discount.IsActive && 
                                      discount.StartDate <= now && 
                                      discount.EndDate >= now && 
                                      discount.DeletedAt == null;

                _logger.LogInformation($"Discount {dto.Discountid} active status: {isDiscountActive} (IsActive: {discount.IsActive}, StartDate: {discount.StartDate}, EndDate: {discount.EndDate}, DeletedAt: {discount.DeletedAt})");

                // Validate all products exist and are not deleted
                var existingProducts = await _unitOfWork.Product.GetAll()
                    .Where(p => dto.ProductsId.Contains(p.Id) && p.DeletedAt == null)
                    .ToListAsync();

                if (existingProducts.Count != dto.ProductsId.Count)
                {
                    var existingIds = existingProducts.Select(p => p.Id).ToList();
                    var missingIds = dto.ProductsId.Except(existingIds).ToList();
                    return Result<List<ProductDto>>.Fail($"Some products not found or deleted: {string.Join(", ", missingIds)}", 404);
                }

                // Check if any products already have a discount
                var productsWithExistingDiscount = existingProducts.Where(p => p.DiscountId != null).ToList();
                var warnings = new List<string>();

                if (productsWithExistingDiscount.Any())
                {
                    var existingDiscountIds = productsWithExistingDiscount.Select(p => p.Id).ToList();
                    warnings.Add($"Products {string.Join(", ", existingDiscountIds)} already have discounts and will be updated.");
                }

                // Apply discount to all products
                foreach (var product in existingProducts)
                {
                    product.DiscountId = dto.Discountid;
                }
				_unitOfWork.Product.UpdateList(existingProducts);

				// Log admin operation
				var adminOpResult = await _adminOpreationServices.AddAdminOpreationAsync(
                    $"Applied discount '{discount.Name}' ({dto.Discountid}) to {existingProducts.Count} products: {string.Join(", ", dto.ProductsId)}",
                    Opreations.AddOpreation,
                    userId,
                    dto.ProductsId.FirstOrDefault()
                );

                if (adminOpResult == null || !adminOpResult.Success)
                {
                    await transaction.RollbackAsync();
                    return Result<List<ProductDto>>.Fail("Failed to log admin operation. Discount application rolled back.", 500);
                }

                await _unitOfWork.CommitAsync();
                await transaction.CommitAsync();

                // Only update cart and orders if discount is active
                if (isDiscountActive)
                {
                    _logger.LogInformation($"Discount {dto.Discountid} is active. Updating cart and order prices for products: {string.Join(", ", dto.ProductsId)}");
                    _backgroundJobClient.Enqueue(() => _cartServices.UpdateCartItemsForProductsAfterAddDiscountAsync(dto.ProductsId, discount.DiscountPercent));
                }
                else
                {
                    _logger.LogInformation($"Discount {dto.Discountid} is not active. Skipping cart and order price updates.");
                }

				// Clear cache
				RemoveCacheAndRelatedCaches();
				var updatedProducts = _unitOfWork.Product
					.GetAll()
					.Where(p => dto.ProductsId.Contains(p.Id));
				var prodcutsdto =await  _productMapper.maptoProductDtoexpression(updatedProducts).ToListAsync();
                   
                var message = $"Successfully applied discount to {existingProducts.Count} products";
                if (!isDiscountActive)
                {
                    message += " (discount is not currently active)";
                }
                if (warnings.Any())
                {
                    message += $". Warnings: {string.Join("; ", warnings)}";
                }

                _logger.LogInformation($"Successfully applied discount {dto.Discountid} to {existingProducts.Count} products. Active: {isDiscountActive}");
                return Result<List<ProductDto>>.Ok(prodcutsdto, message, 200, warnings: warnings);
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, $"Error in ApplyDiscountToProductsAsync for discount {dto.Discountid} and products {string.Join(", ", dto.ProductsId)}");
                _backgroundJobClient.Enqueue(() => _errorNotificationService.SendErrorNotificationAsync(ex.Message, ex.StackTrace));
                return Result<List<ProductDto>>.Fail("Error applying discount to products.", 500);
            }
        }

		public async Task<Result<List<ProductDto>>> RemoveDiscountFromProductsAsync(List<int> productIds, string userId)
		{
			_logger.LogInformation($"Removing discounts from {productIds?.Count ?? 0} products");
			
			if (productIds == null || !productIds.Any())
				return Result<List<ProductDto>>.Fail("Product IDs cannot be null or empty.", 400);
			
			if (string.IsNullOrWhiteSpace(userId))
				return Result<List<ProductDto>>.Fail("User ID cannot be null or empty.", 400);

			using var transaction = await _unitOfWork.BeginTransactionAsync();
			try
			{
				// Validate all products exist and are not deleted
				var existingProducts = await _unitOfWork.Product.GetAll()
					.Where(p => productIds.Contains(p.Id) && p.DeletedAt == null)
					.ToListAsync();

				if (existingProducts.Count != productIds.Count)
				{
					var existingIds = existingProducts.Select(p => p.Id).ToList();
					var missingIds = productIds.Except(existingIds).ToList();
					return Result<List<ProductDto>>.Fail($"Some products not found or deleted: {string.Join(", ", missingIds)}", 404);
				}

				// Check which products have discounts to remove
				var productsWithDiscounts = existingProducts.Where(p => p.DiscountId != null).ToList();
				var productsWithoutDiscounts = existingProducts.Where(p => p.DiscountId == null).ToList();
				var warnings = new List<string>();

				if (productsWithoutDiscounts.Any())
				{
					var noDiscountIds = productsWithoutDiscounts.Select(p => p.Id).ToList();
					warnings.Add($"Products {string.Join(", ", noDiscountIds)} don't have discounts to remove.");
				}

				if (!productsWithDiscounts.Any())
				{
					return Result<List<ProductDto>>.Fail("None of the specified products have discounts to remove.", 400);
				}

				// Remove discounts from products that have them
				foreach (var product in productsWithDiscounts)
				{
					product.DiscountId = null;
				}

				_unitOfWork.Product.UpdateList(productsWithDiscounts);
				// Log admin operation
				var adminOpResult = await _adminOpreationServices.AddAdminOpreationAsync(
					$"Removed discounts from {productsWithDiscounts.Count} products: {string.Join(", ", productsWithDiscounts.Select(p => p.Id))}",
					Opreations.DeleteOpreation,
					userId,
					productIds.FirstOrDefault()
				);

				if (adminOpResult == null || !adminOpResult.Success)
				{
					await transaction.RollbackAsync();
					return Result<List<ProductDto>>.Fail("Failed to log admin operation. Discount removal rolled back.", 500);
				}

			
				_backgroundJobClient.Enqueue(() => _cartServices.UpdateCartItemsForProductsAfterRemoveDiscountAsync(productIds));
				await _unitOfWork.CommitAsync();
				await transaction.CommitAsync();

				RemoveCacheAndRelatedCaches();


				var updatedProducts = _unitOfWork.Product
					.GetAll()
					.Where(p => productIds.Contains(p.Id));
					var productDtos = await _productMapper.maptoProductDtoexpression(updatedProducts).ToListAsync();
				var message = $"Successfully removed discounts from {productsWithDiscounts.Count} products";
				if (warnings.Any())
				{
					message += $". Warnings: {string.Join("; ", warnings)}";
				}

				_logger.LogInformation($"Successfully removed discounts from {productsWithDiscounts.Count} products");
				return Result<List<ProductDto>>.Ok(productDtos, message, 200, warnings: warnings);
			}
			catch (Exception ex)
			{
				await transaction.RollbackAsync();
				_logger.LogError(ex, $"Error in RemoveDiscountFromProductsAsync for products {string.Join(", ", productIds)}");
				_backgroundJobClient.Enqueue(() => _errorNotificationService.SendErrorNotificationAsync(ex.Message, ex.StackTrace));
				return Result<List<ProductDto>>.Fail("Error removing discounts from products.", 500);
			}
		}

		public async Task<Result<DiscountDto>> GetProductDiscountAsync(int productId)
		{
			try
			{

				if(! await _unitOfWork.Product.IsExsistAndHasDiscountAsync(productId))
				{
					  return Result<DiscountDto>.Fail("Product not found Or No discount found for this product", 404);
				}

				var discount = await _unitOfWork.Product.GetDiscountofProduct(productId);
				if(discount==null)
					return Result<DiscountDto>.Fail("Product not found Or No discount found for this product", 404);
			







				var discountDto = new DiscountDto
				{
					Id = discount.Id,
					Name = discount.Name,
					Description = discount.Description,
					DiscountPercent = discount.DiscountPercent,
					StartDate = discount.StartDate,
					EndDate = discount.EndDate,
					IsActive = discount.IsActive,
					CreatedAt = discount.CreatedAt,
					ModifiedAt = discount.ModifiedAt,
					DeletedAt = discount.DeletedAt,
				};

				return Result<DiscountDto>.Ok(discountDto, "Product discount retrieved successfully", 200);
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, $"Error in GetProductDiscountAsync for productId: {productId}");
				_backgroundJobClient.Enqueue(() => _errorNotificationService.SendErrorNotificationAsync(ex.Message, ex.StackTrace));
				return Result<DiscountDto>.Fail("Error retrieving product discount", 500);
			}
		}




		public async Task<Result<bool>> AddDiscountToProductAsync(int productId, int discountId, string userId)
		{
			_logger.LogInformation($"Adding discount to product: {productId} with discount: {discountId}");
				using var transaction = await _unitOfWork.BeginTransactionAsync();
			try
			{
				var product = await _unitOfWork.Product.IsExsistAsync(productId);
				if (!product )
					return Result<bool>.Fail("Product not found", 404);

				var discount = await _unitOfWork.Repository<Models.Discount>().GetByIdAsync(discountId);
				if (discount == null || discount.DeletedAt != null)
					return Result<bool>.Fail("Discount not found or deleted", 404);
				var now = DateTime.UtcNow;
				var isDiscountActive = discount.IsActive && 
									  discount.StartDate <= now && 
									  discount.EndDate >= now && 
									  discount.DeletedAt == null;

				_logger.LogInformation($"Discount {discountId} active status: {isDiscountActive} (IsActive: {discount.IsActive}, StartDate: {discount.StartDate}, EndDate: {discount.EndDate}, DeletedAt: {discount.DeletedAt})");


				if( !await _unitOfWork.Product.AddDiscountToProductAsync(productId, discountId))
				{
					return Result<bool>.Fail("Product not found", 404);
				}
				
			

                var adminOpResult = await _adminOpreationServices.AddAdminOpreationAsync(
                    $"Add Discount to Product {productId} , Discount id: {discountId}",
                    Opreations.AddOpreation,
                    userId,
                    productId
                );
                if (adminOpResult == null || !adminOpResult.Success)
                {
                    await transaction.RollbackAsync();
                    return Result<bool>.Fail(" log admin operation. Discount application rolled back.", 500);
                }
				
				await _unitOfWork.CommitAsync();
				await transaction.CommitAsync();

				// Only update cart and orders if discount is active
				if (isDiscountActive)
				{
					_logger.LogInformation($"Discount {discountId} is active. Updating cart and order prices for product: {productId}");
					
					_backgroundJobClient.Enqueue(() => _cartServices.UpdateCartItemsForProductsAfterAddDiscountAsync(new List<int> { productId }, discount.DiscountPercent));
				}
				else
				{
					_logger.LogInformation($"Discount {discountId} is not active. Skipping cart and order price updates for product: {productId}");
				}

				RemoveCacheAndRelatedCaches();

				var message = "Discount added successfully";
				if (!isDiscountActive)
				{
					message += " (discount is not currently active)";
				}

				_logger.LogInformation($"Successfully added discount {discountId} to product {productId}. Active: {isDiscountActive}");
				return Result<bool>.Ok(true, message, 201);
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, $"Error in AddDiscountToProductAsync for productId: {productId}, discountId: {discountId}");
			 	_backgroundJobClient.Enqueue(()=> _errorNotificationService.SendErrorNotificationAsync(ex.Message, ex.StackTrace));
				return Result<bool>.Fail("Error adding discount", 500);
			}
		}

		public async Task<Result<bool>> UpdateProductDiscountAsync(int productId, int discountId, string userId)
		{
			_logger.LogInformation($"[UpdateProductDiscountAsync] Called with productId={productId}, discountId={discountId}, userId={userId}");
			// Input validation
			if (productId <= 0)
			{
				_logger.LogWarning("[UpdateProductDiscountAsync] Invalid product ID: {productId}", productId);
				return Result<bool>.Fail("Invalid product ID.", 400);
			}
			if (discountId <= 0)
			{
				_logger.LogWarning("[UpdateProductDiscountAsync] Invalid discount ID: {discountId}", discountId);
				return Result<bool>.Fail("Invalid discount ID.", 400);
			}
			if (string.IsNullOrWhiteSpace(userId))
			{
				_logger.LogWarning("[UpdateProductDiscountAsync] User ID is null or empty.");
				return Result<bool>.Fail("User ID cannot be null or empty.", 400);
			}

			try
			{
				
				if (!await _unitOfWork.Product.IsExsistAsync(productId))
				{
					_logger.LogWarning($"[UpdateProductDiscountAsync] Product not found: {productId}");
					return Result<bool>.Fail("Product not found", 404);
				}
			
				var discount = await _unitOfWork.Repository<Models.Discount>().GetByIdAsync(discountId);
				if (discount == null || discount.DeletedAt != null)
				{
					_logger.LogWarning($"[UpdateProductDiscountAsync] Discount not found or deleted: {discountId}");
					return Result<bool>.Fail("Discount not found or deleted", 404);
				}

				// Check if discount is active and valid
				var now = DateTime.UtcNow;
				var isDiscountActive = discount.IsActive && 
									  discount.StartDate <= now && 
									  discount.EndDate >= now && 
									  discount.DeletedAt == null;

				_logger.LogInformation($"[UpdateProductDiscountAsync] Discount {discountId} active status: {isDiscountActive} (IsActive: {discount.IsActive}, StartDate: {discount.StartDate}, EndDate: {discount.EndDate}, DeletedAt: {discount.DeletedAt})");

				using var transaction = await _unitOfWork.BeginTransactionAsync();
				
					if(!await _unitOfWork.Product.AddDiscountToProductAsync(productId,discountId))
					{
						return Result<bool>.Fail("Error updating discount", 500);

					}

			

				var adminOpResult = await _adminOpreationServices.AddAdminOpreationAsync(
					$"Update Discount for Product {productId}",
					Opreations.UpdateOpreation,
					userId,
					productId
				);
				if (adminOpResult == null || !adminOpResult.Success)
				{
					await transaction.RollbackAsync();
					_logger.LogWarning($"[UpdateProductDiscountAsync] Failed to log admin operation for productId={productId}");
					return Result<bool>.Fail("Failed to log admin operation. Discount update rolled back.", 500);
				}

				await _unitOfWork.CommitAsync();
				await transaction.CommitAsync();

				// Only update cart and orders if discount is active
				if (isDiscountActive)
				{
					_logger.LogInformation($"[UpdateProductDiscountAsync] Discount {discountId} is active. Updating cart and order prices for product: {productId}");
				
					_backgroundJobClient.Enqueue(() => _cartServices.UpdateCartItemsForProductsAfterAddDiscountAsync(new List<int> { productId }, discount.DiscountPercent));
				}
				else
				{
					_logger.LogInformation($"[UpdateProductDiscountAsync] Discount {discountId} is not active. Skipping cart and order price updates for product: {productId}");
				}

				RemoveCacheAndRelatedCaches();

				var message = "Discount updated successfully";
				if (!isDiscountActive)
				{
					message += " (discount is not currently active)";
				}

				_logger.LogInformation($"[UpdateProductDiscountAsync] Discount updated successfully for productId={productId}. Active: {isDiscountActive}");
				return Result<bool>.Ok(true, message, 200);
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, $"Error in UpdateProductDiscountAsync for productId: {productId}, discountId: {discountId}");
			 	_backgroundJobClient.Enqueue(()=> _errorNotificationService.SendErrorNotificationAsync(ex.Message, ex.StackTrace));
				return Result<bool>.Fail("Error updating discount", 500);
			}
		}

		public async Task<Result<bool>> RemoveDiscountFromProductAsync(int productId, string userId)
		{
			_logger.LogInformation($"[RemoveDiscountFromProductAsync] Called with productId={productId}, userId={userId}");
		
			if (productId <= 0)
			{
				_logger.LogWarning("[RemoveDiscountFromProductAsync] Invalid product ID: {productId}", productId);
				return Result<bool>.Fail("Invalid product ID.", 400);
			}
			if (string.IsNullOrWhiteSpace(userId))
			{
				_logger.LogWarning("[RemoveDiscountFromProductAsync] User ID is null or empty.");
				return Result<bool>.Fail("User ID cannot be null or empty.", 400);
			}

			try
			{
				var productExists = await _unitOfWork.Product.GetByIdAsync(productId);
				if (productExists==null||productExists.DiscountId==null)
				{
					_logger.LogWarning($"[RemoveDiscountFromProductAsync] Product not found or has no discount to remove: {productId}");
					return Result<bool>.Fail("Product not found Or Product has no discount to remove", 404);
				}

				using var transaction = await _unitOfWork.BeginTransactionAsync();
				productExists.DiscountId = null;
				var adminOpResult = await _adminOpreationServices.AddAdminOpreationAsync(
					$"Remove Discount from Product {productId}",
					Opreations.DeleteOpreation,
					userId,
					productId
				);
				if (adminOpResult == null || !adminOpResult.Success)
				{
					await transaction.RollbackAsync();
					_logger.LogWarning($"[RemoveDiscountFromProductAsync] Failed to log admin operation for productId={productId}");
					return Result<bool>.Fail("Failed to log admin operation. Discount removal rolled back.", 500);
				}
				_backgroundJobClient.Enqueue(() => _cartServices.UpdateCartItemsForProductsAfterRemoveDiscountAsync(new List<int> { productId }) );
				await _unitOfWork.CommitAsync();
				await transaction.CommitAsync();
				RemoveCacheAndRelatedCaches();
				_SubCategoryCacheHelper.ClearSubCategoryDataCache();

				_logger.LogInformation($"[RemoveDiscountFromProductAsync] Discount removed successfully for productId={productId}");
				return Result<bool>.Ok(true, "Discount removed successfully", 200);
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, $"Error in RemoveDiscountFromProductAsync for productId: {productId}");
			 	_backgroundJobClient.Enqueue(()=> _errorNotificationService.SendErrorNotificationAsync(ex.Message, ex.StackTrace));
				return Result<bool>.Fail("Error removing discount", 500);
			}
		}

		
		public async Task<Result<List<ProductDto>>> GetProductsWithActiveDiscountsAsync(bool? isactive)
		{
			try
			{
				var now = DateTime.UtcNow;

				var products = _unitOfWork.Product.GetAll()
					.Where(p => p.Discount != null
						&& p.Discount.IsActive
						&& p.Discount.DeletedAt == null
						&& p.Discount.StartDate <= now
						&& p.Discount.EndDate > now
						&& p.DeletedAt==null
						);
				if (isactive.HasValue)
				products=	products.Where(p=>p.IsActive==isactive.Value);

				var productDtos = await _productMapper.maptoProductDtoexpression(products).ToListAsync();


				if (!products.Any())
					return Result<List<ProductDto>>.Fail("No products with active discounts found", 404);

				return Result<List<ProductDto>>.Ok(productDtos, "Products with active discounts retrieved successfully", 200);
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error in GetProductsWithActiveDiscountsAsync");
			 	_backgroundJobClient.Enqueue(()=> _errorNotificationService.SendErrorNotificationAsync(ex.Message, ex.StackTrace));
				return Result<List<ProductDto>>.Fail("Error retrieving products with active discounts", 500);
			}
		}


	
	}
} 