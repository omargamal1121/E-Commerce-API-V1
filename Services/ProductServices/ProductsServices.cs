using AutoMapper;
using E_Commerce.DtoModels.CategoryDtos;
using E_Commerce.DtoModels.DiscoutDtos;
using E_Commerce.DtoModels.ImagesDtos;
using E_Commerce.DtoModels.ProductDtos;
using E_Commerce.DtoModels.Responses;
using E_Commerce.Enums;
using E_Commerce.ErrorHnadling;
using E_Commerce.Interfaces;
using E_Commerce.Models;
using E_Commerce.Services.AdminOpreationServices;
using E_Commerce.Services.EmailServices;
using E_Commerce.UOW;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using System.Collections.Generic;
using System.Threading.Tasks;

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
		Task<Result<bool>> UpdateVariantQuantityAsync(int variantId, int newQuantity, string userId);
		Task<Result<bool>> ActivateProductAsync(int productId, string userId);
		Task<Result<bool>> DeactivateProductAsync(int productId, string userId);

		// Discount operations (delegated to ProductDiscountService)
		Task<Result<DiscountDto>> GetProductDiscountAsync(int productId);
		Task<Result<bool>> AddDiscountToProductAsync(int productId, int discountId, string userId);
		Task<Result<bool>> UpdateProductDiscountAsync(int productId, int discountId, string userId);
		Task<Result<bool>> RemoveDiscountFromProductAsync(int productId, string userId);
		Task<Result<List<ProductDto>>> GetProductsWithActiveDiscountsAsync();
		public Task<Result<List<ProductDto>>> ApplyDiscountToProductsAsync(ApplyDiscountToProductsDto dto, string userid);
		public Task<Result<List<ProductDto>>> RemoveDiscountFromProductsAsync(List<int> productIds, string userId);
	}

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
		public async Task<Result<ProductDetailDto>> GetProductByIdAsync(int id, bool? isActive = null, bool? deletedOnly = null)
		{
			return await _productCatalogService.GetProductByIdAsync(id, isActive, deletedOnly);
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
		public async Task<Result<List<ProductDto>>> GetProductsBySubCategoryId(int subCategoryId, bool? isActive, bool? deletedOnly)
		{
			return await _productCatalogService.GetProductsBySubCategoryId(subCategoryId, isActive, deletedOnly);
		}
		// Search Operations
		public async Task<Result<List<ProductDto>>> GetNewArrivalsAsync(int page, int pageSize, bool? isActive = null, bool? deletedOnly = null)
		{
			return await _productSearchService.GetNewArrivalsAsync(page, pageSize, isActive, deletedOnly);
		}
		public async Task<Result<List<ProductDto>>> GetBestSellersAsync(int page, int pageSize, bool? isActive = null, bool? deletedOnly = null)
		{
			return await _productSearchService.GetBestSellersAsync(page, pageSize, isActive, deletedOnly);
		}
		public async Task<Result<List<BestSellingProductDto>>> GetBestSellersProductsWithCountAsync(int page, int pageSize, bool? isActive = null, bool? deletedOnly = null)
		{
			return await _productSearchService.GetBestSellerProductsWithCountAsync(isActive,deletedOnly, page, pageSize);
		}
		public async Task<Result<List<ProductDto>>> AdvancedSearchAsync(AdvancedSearchDto searchCriteria, int page, int pageSize, bool? isActive = null, bool? deletedOnly = null)
		{
			return await _productSearchService.AdvancedSearchAsync(searchCriteria, page, pageSize, isActive, deletedOnly);
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
	
		public async Task<Result<bool>> UpdateVariantQuantityAsync(int variantId, int newQuantity, string userId)
		{
			return await _productVariantService.UpdateVariantQuantityAsync(variantId, newQuantity, userId);
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

		public async Task<Result<List<ProductDto>>> GetProductsWithActiveDiscountsAsync()
		{
			return await _productDiscountService.GetProductsWithActiveDiscountsAsync();
		}

		public async Task<Result<List<ProductDto>>> ApplyDiscountToProductsAsync(ApplyDiscountToProductsDto dto,string userid)
		{
			return await _productDiscountService.ApplyDiscountToProductsAsync(dto,userid);
		}

		public async Task<Result<List<ProductDto>>> RemoveDiscountFromProductsAsync(List<int> productIds, string userId)
		{
			return await _productDiscountService.RemoveDiscountFromProductsAsync(productIds, userId);
		}
	}
}
