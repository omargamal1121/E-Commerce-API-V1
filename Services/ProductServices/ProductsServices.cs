
using E_Commerce.DtoModels.DiscoutDtos;
using E_Commerce.DtoModels.ImagesDtos;
using E_Commerce.DtoModels.ProductDtos;
using E_Commerce.Services.ProductVariantServices;


namespace E_Commerce.Services.ProductServices
{

	public class ProductsServices : IProductsServices
	{
		private readonly IProductCatalogService _productCatalogService;
		private readonly IProductSearchService _productSearchService;
		private readonly IProductImageService _productImageService;
		private readonly IProductVariantService _productVariantService;
		private readonly IProductDiscountService _productDiscountService;
		private readonly ILogger<ProductsServices> _logger;

		public ProductsServices(
			IProductCatalogService productCatalogService,
			IProductSearchService productSearchService,
			IProductImageService productImageService,
			IProductVariantService productVariantService,
			IProductDiscountService productDiscountService,
			ILogger<ProductsServices> logger)
		{
			_productCatalogService = productCatalogService;
			_productSearchService = productSearchService;
			_productImageService = productImageService;
			_productVariantService = productVariantService;
			_productDiscountService = productDiscountService;
			_logger = logger;
		}

		// Core Product Operations
		public async Task<Result<ProductDetailDto>> GetProductByIdAsync(int id, bool? isActive = null, bool? deletedOnly = null, bool IsAdmin = false)
		{
			_logger.LogInformation($"Fetching product details for Product ID: {id}, IsActive: {isActive}, DeletedOnly: {deletedOnly}, IsAdmin: {IsAdmin}");
			return await _productCatalogService.GetProductByIdAsync(id, isActive, deletedOnly, IsAdmin);
		}
		public async Task<Result<ProductDto>> CreateProductAsync(CreateProductDto dto, string userId)
		{
			return await _productCatalogService.CreateProductAsync(dto, userId);
		}
		public async Task<Result<ProductDto>> UpdateProductAsync(int id, UpdateProductDto dto, string userId)
		{
			return await _productCatalogService.UpdateProductAsync(id, dto, userId);
		}
		public async Task<Result<bool>> DeleteProductAsync(int id, string userId)
		{
			return await _productCatalogService.DeleteProductAsync(id, userId);
		}
		public async Task<Result<bool>> RestoreProductAsync(int id, string userId)
		{
			return await _productCatalogService.RestoreProductAsync(id, userId);
		}
		public async Task<Result<List<ProductDto>>> GetProductsBySubCategoryIdAsync(int subCategoryId, bool? isActive = null, bool? deletedOnly = null, int page = 1, int pageSize = 10, bool IsAdmin = false)
		{
			return await _productSearchService.AdvancedSearchAsync(new AdvancedSearchDto { Subcategoryid = subCategoryId }, page, pageSize, isActive, deletedOnly, IsAdmin);
		}
		public async Task<Result<List<ProductDto>>> GetNewArrivalsAsync(int page = 1, int pageSize = 10, bool? isActive = null, bool? deletedOnly = null, bool IsAdmin = false)
		{
			return await _productSearchService.GetNewArrivalsAsync(page, pageSize, isActive, deletedOnly, IsAdmin);
		}
		public async Task<Result<List<ProductDto>>> GetProductsAsync(string? search = null, bool? isActive = null, bool? deletedOnly = null, int page = 1, int pageSize = 10, bool IsAdmin = false)
		{
			return await _productSearchService.AdvancedSearchAsync(new AdvancedSearchDto { SearchTerm = search }, page, pageSize, isActive, deletedOnly, IsAdmin);
		}
		public async Task<Result<List<BestSellingProductDto>>> GetBestSellersProductsWithCountAsync(int page, int pageSize, bool? isActive = null, bool? deletedOnly = null,bool IsAdmin=false)
		{
			return await _productSearchService.GetBestSellerProductsWithCountAsync(isActive,deletedOnly, page, pageSize,IsAdmin);
		}
		public async Task<Result<List<ProductDto>>> AdvancedSearchAsync(AdvancedSearchDto searchCriteria, int page = 1, int pageSize = 10, bool? isActive = null, bool? deletedOnly = null, bool IsAdmin = false)
		{
			return await _productSearchService.AdvancedSearchAsync(searchCriteria, page, pageSize, isActive, deletedOnly, IsAdmin);
		}
		// Image Operations
		public async Task<Result<List<ImageDto>>> GetProductImagesAsync(int productId)
		{
			return await _productImageService.GetProductImagesAsync(productId);
		}
		public async Task<Result<List<ImageDto>>> AddProductImagesAsync(int productId, List<IFormFile> images, string userId)
		{
			return await _productImageService.AddProductImagesAsync(productId, images, userId);
		}
		public async Task<Result<bool>> RemoveProductImageAsync(int productId, int imageId, string userId)
		{
			return await _productImageService.RemoveProductImageAsync(productId, imageId, userId);
		}
		public async Task<Result<ImageDto>> UploadAndSetMainImageAsync(int productId, IFormFile mainImage, string userId)
		{
			return await _productImageService.UploadAndSetMainImageAsync(productId, mainImage, userId);
		}
		// Variant Operations
		public async Task<Result<List<ProductVariantDto>>> GetProductVariantsAsync(int productId)
		{
			return await _productVariantService.GetProductVariantsAsync(productId);
		}
		public async Task<Result<ProductVariantDto>> AddVariantAsync(int productId, CreateProductVariantDto dto, string userId)
		{
			return await _productVariantService.AddVariantAsync(productId, dto, userId);
		}
		public async Task<Result<ProductVariantDto>> UpdateVariantAsync(int variantId, UpdateProductVariantDto dto, string userId)
		{
			return await _productVariantService.UpdateVariantAsync(variantId, dto, userId);
		}
		public async Task<Result<bool>> DeleteVariantAsync(int variantId, string userId)
		{
			return await _productVariantService.DeleteVariantAsync(variantId, userId);
		}
	
	
		// Discount Operations
		public async Task<Result<DiscountDto>> GetProductDiscountAsync(int productId)
		{
			return await _productDiscountService.GetProductDiscountAsync(productId);
		}
		public async Task<Result<bool>> AddDiscountToProductAsync(int productId, int discountId, string userId)
		{
			return await _productDiscountService.AddDiscountToProductAsync(productId, discountId, userId);
		}
		public async Task<Result<bool>> UpdateProductDiscountAsync(int productId, int discountId, string userId)
		{
			return await _productDiscountService.UpdateProductDiscountAsync(productId, discountId, userId);
		}
		public async Task<Result<bool>> RemoveDiscountFromProductAsync(int productId, string userId)
		{
			return await _productDiscountService.RemoveDiscountFromProductAsync(productId, userId);
		}

		public async Task<Result<bool>> ActivateProductAsync(int productId, string userId)
		{
			return	await _productCatalogService.ActivateProductAsync(productId, userId);
		}

		public async Task<Result<bool>> DeactivateProductAsync(int productId, string userId)
		{
			return await _productCatalogService.DeactivateProductAsync(productId, userId);
		}

		public async Task<Result<List<ProductDto>>> GetProductsWithActiveDiscountsAsync(bool? IsActive)
		{
			return await _productDiscountService.GetProductsWithActiveDiscountsAsync(IsActive);
		}

		public async Task<Result<List<ProductDto>>> ApplyDiscountToProductsAsync(ApplyDiscountToProductsDto dto,string userid)
		{
			return await _productDiscountService.ApplyDiscountToProductsAsync(dto,userid);
		}

		public async Task<Result<List<ProductDto>>> RemoveDiscountFromProductsAsync(List<int> productIds, string userId)
		{
			return await _productDiscountService.RemoveDiscountFromProductsAsync(productIds, userId);
		}


		public async Task<Result<List<ProductDto>>> GetBestSellersAsync(int page = 1, int pageSize = 10, bool? isActive = null, bool? deletedOnly = null, bool IsAdmin = false)
		{
			return await _productSearchService.GetBestSellersAsync(page,pageSize, isActive, deletedOnly, IsAdmin);
		}
	}
}
