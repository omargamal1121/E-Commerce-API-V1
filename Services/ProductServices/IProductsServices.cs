using E_Commerce.DtoModels.DiscoutDtos;
using E_Commerce.DtoModels.ImagesDtos;
using E_Commerce.DtoModels.ProductDtos;

namespace E_Commerce.Services.ProductServices
{
	public interface IProductsServices
	{
		// Core product operations (delegated to ProductCatalogService)
		Task<Result<ProductDetailDto>> GetProductByIdAsync(int id, bool? isActive = null, bool? deletedOnly = null);
		Task<Result<ProductDto>> CreateProductAsync(CreateProductDto dto, string userId);
		public  Task<Result<List<BestSellingProductDto>>> GetBestSellersProductsWithCountAsync(int page, int pageSize, bool? isActive = null, bool? deletedOnly = null);
		Task<Result<ProductDto>> UpdateProductAsync(int id, UpdateProductDto dto, string userId);
		Task<Result<bool>> DeleteProductAsync(int id, string userId);
		Task<Result<bool>> RestoreProductAsync(int id, string userId);
		Task<Result<List<ProductDto>>> GetProductsBySubCategoryId(int subCategoryId, bool? isActive, bool? deletedOnly);
		// Search operations (delegated to ProductSearchService)
		Task<Result<List<ProductDto>>> GetNewArrivalsAsync(int page, int pageSize, bool? isActive = null, bool? deletedOnly = null);
		Task<Result<List<ProductDto>>> GetBestSellersAsync(int page, int pageSize, bool? isActive = null, bool? deletedOnly = null);
		Task<Result<List<ProductDto>>> AdvancedSearchAsync(AdvancedSearchDto searchCriteria, int page, int pageSize, bool? isActive = null, bool? deletedOnly = null);
		// Image operations (delegated to ProductImageService)
		Task<Result<List<ImageDto>>> GetProductImagesAsync(int productId);
		Task<Result<List<ImageDto>>> AddProductImagesAsync(int productId, List<IFormFile> images, string userId);
		Task<Result<bool>> RemoveProductImageAsync(int productId, int imageId, string userId);
		Task<Result<ImageDto>> UploadAndSetMainImageAsync(int productId, IFormFile mainImage, string userId);
		// Variant operations (delegated to ProductVariantService)
		Task<Result<List<ProductVariantDto>>> GetProductVariantsAsync(int productId);
		Task<Result<ProductVariantDto>> AddVariantAsync(int productId, CreateProductVariantDto dto, string userId);
		Task<Result<ProductVariantDto>> UpdateVariantAsync(int variantId, UpdateProductVariantDto dto, string userId);
		Task<Result<bool>> DeleteVariantAsync(int variantId, string userId);
		Task<Result<bool>> ActivateProductAsync(int productId, string userId);
		Task<Result<bool>> DeactivateProductAsync(int productId, string userId);

		// Discount operations (delegated to ProductDiscountService)
		Task<Result<DiscountDto>> GetProductDiscountAsync(int productId);
		Task<Result<bool>> AddDiscountToProductAsync(int productId, int discountId, string userId);
		Task<Result<bool>> UpdateProductDiscountAsync(int productId, int discountId, string userId);
		Task<Result<bool>> RemoveDiscountFromProductAsync(int productId, string userId);
		Task<Result<List<ProductDto>>> GetProductsWithActiveDiscountsAsync(bool?IsActive);
		public Task<Result<List<ProductDto>>> ApplyDiscountToProductsAsync(ApplyDiscountToProductsDto dto, string userid);
		public Task<Result<List<ProductDto>>> RemoveDiscountFromProductsAsync(List<int> productIds, string userId);
	}
}
