using E_Commerce.DtoModels;
using E_Commerce.DtoModels.CategoryDtos;
using E_Commerce.DtoModels.DiscoutDtos;
using E_Commerce.DtoModels.ProductDtos;
using E_Commerce.Enums;
using E_Commerce.Services;
using E_Commerce.Models;
using E_Commerce.UOW;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System.Security.Claims;
using E_Commerce.Interfaces;
using E_Commerce.ErrorHnadling;
using E_Commerce.DtoModels.Responses;
using E_Commerce.Services.ProductServices;
using E_Commerce.DtoModels.ImagesDtos;

namespace E_Commerce.Controllers
{
	[Route("api/[controller]s")]
	[ApiController]
	public class ProductController : ControllerBase
	{
		private readonly IProductsServices _productsServices;
		private readonly ILogger<ProductController> _logger;
		public ProductController(IProductsServices productsServices, ILogger<ProductController> logger)
		{
			_productsServices = productsServices;
			_logger = logger;
		}

		private ActionResult<ApiResponse<T>> HandleResult<T>(Result<T> result, string actionName = null, int? id = null) 
		{
			var apiResponse = result.Success
				? ApiResponse<T>.CreateSuccessResponse(result.Message, result.Data, result.StatusCode, warnings: result.Warnings)
				: ApiResponse<T>.CreateErrorResponse(result.Message, new ErrorResponse("Error", result.Message), result.StatusCode, warnings: result.Warnings);

			switch (result.StatusCode)
			{
				case 200:
					return Ok(apiResponse);
				case 201:
					return actionName != null && id.HasValue ? CreatedAtAction(actionName, new { id }, apiResponse) : StatusCode(201, apiResponse);
				case 400:
					return BadRequest(apiResponse);
				case 401:
					return Unauthorized(apiResponse);
				case 404:
					return NotFound(apiResponse);
				case 409:
					return Conflict(apiResponse);
				default:
					return StatusCode(result.StatusCode, apiResponse);
			}
		}

		[HttpGet("best-selling")]
		[Authorize(Roles ="Admin")]
		public async Task<ActionResult<ApiResponse<List< BestSellingProductDto>>>> GetBestSellingProducts(
	[FromQuery] int page = 1,
	[FromQuery] int pageSize = 10,
	[FromQuery] bool? isDeleted = null,
	[FromQuery] bool? isActive = null)
		{
			var result = await _productsServices.GetBestSellersProductsWithCountAsync(page,pageSize, isActive,isDeleted);
			return HandleResult<List<BestSellingProductDto>>(result, nameof(GetBestSellingProducts));
			
		}


		[HttpGet("{id}/discount")]
		public async Task<ActionResult<ApiResponse<DiscountDto>>> GetProductDiscount(int id)
		{
			var response = await _productsServices.GetProductDiscountAsync(id);
			return HandleResult(response, nameof(GetProductDiscount), id);
		}

		[HttpPost("{id}/discount")]
		[Authorize(Roles = "Admin")]
		public async Task<ActionResult<ApiResponse<bool>>> AddDiscountToProduct(int id, [FromBody] int discountId)
		{
			if (!ModelState.IsValid || discountId <= 0)
			{
				var errors = string.Join(", ", ModelState.Values
					.SelectMany(v => v.Errors)
					.Select(e => e.ErrorMessage)
					.ToList());
				_logger.LogError($"Validation Errors: {errors}");
				return BadRequest(ApiResponse<ProductDetailDto>.CreateErrorResponse("Invalid discount data", new ErrorResponse("Invalid data", errors ?? "Invalid discount ID")));
			}
			var userId = HttpContext.Items["UserId"]?.ToString();
			var response = await _productsServices.AddDiscountToProductAsync(id, discountId, userId);
			return HandleResult(response, nameof(AddDiscountToProduct), id);
		}

		[HttpPut("{id}/discount")]
		[Authorize(Roles = "Admin")]
		public async Task<ActionResult<ApiResponse<bool>>> UpdateProductDiscount(int id, [FromBody] int discountId)
		{
			if (!ModelState.IsValid || discountId <= 0)
			{
				var errors = string.Join(", ", ModelState.Values
					.SelectMany(v => v.Errors)
					.Select(e => e.ErrorMessage)
					.ToList());
				_logger.LogError($"Validation Errors: {errors}");
				return BadRequest(ApiResponse<ProductDetailDto>.CreateErrorResponse("Invalid discount data", new ErrorResponse("Invalid data", errors ?? "Invalid discount ID")));
			}
			var userId = HttpContext.Items["UserId"]?.ToString();
			var response = await _productsServices.UpdateProductDiscountAsync(id, discountId, userId);
			return HandleResult(response, nameof(UpdateProductDiscount), id);
		}

		[HttpDelete("{id}/discount")]
		[Authorize(Roles = "Admin")]
		public async Task<ActionResult<ApiResponse<bool>>> RemoveDiscountFromProduct(int id)
		{
			var userId = HttpContext.Items["UserId"]?.ToString();
			var response = await _productsServices.RemoveDiscountFromProductAsync(id, userId);
			return HandleResult(response, nameof(RemoveDiscountFromProduct), id);
		}

		[HttpPost("bulk/discount")]
		[Authorize(Roles = "Admin")]
		public async Task<ActionResult<ApiResponse<List<ProductDto>>>> ApplyDiscountToProducts([FromBody] ApplyDiscountToProductsDto dto)
		{
			if (!ModelState.IsValid)
			{
				var errors = string.Join(", ", ModelState.Values
					.SelectMany(v => v.Errors)
					.Select(e => e.ErrorMessage)
					.ToList());
				_logger.LogError($"Validation Errors: {errors}");
				return BadRequest(ApiResponse<List<ProductDto>>.CreateErrorResponse("Invalid data", new ErrorResponse("Invalid data", errors)));
			}

			var userId = HttpContext.Items["UserId"]?.ToString();
			var response = await _productsServices.ApplyDiscountToProductsAsync(dto, userId);
			return HandleResult(response, nameof(ApplyDiscountToProducts));
		}

		[HttpDelete("bulk/discount")]
		[Authorize(Roles = "Admin")]
		public async Task<ActionResult<ApiResponse<List<ProductDto>>>> RemoveDiscountFromProducts([FromBody] List<int> productIds)
		{
			if (!ModelState.IsValid || productIds == null || !productIds.Any())
			{
				var errors = string.Join(", ", ModelState.Values
					.SelectMany(v => v.Errors)
					.Select(e => e.ErrorMessage)
					.ToList());
				_logger.LogError($"Validation Errors: {errors}");
				return BadRequest(ApiResponse<List<ProductDto>>.CreateErrorResponse("Invalid data", new ErrorResponse("Invalid data", errors ?? "Product IDs cannot be null or empty")));
			}

			var userId = HttpContext.Items["UserId"]?.ToString();
			var response = await _productsServices.RemoveDiscountFromProductsAsync(productIds, userId);
			return HandleResult(response, nameof(RemoveDiscountFromProducts));
		}
		[HttpGet("{id}")]
		[AllowAnonymous]
		public async Task<ActionResult<ApiResponse<ProductDetailDto>>> GetProduct(
			int id,
			[FromQuery] bool? isActive = null,
			[FromQuery] bool? includeDeleted = null)
		{
			_logger.LogInformation($"Executing {nameof(GetProduct)} for ID: {id}");
			
			bool isAdmin = User?.IsInRole("Admin") == true;
			
			// For non-admin users, restrict to active and non-deleted products
			if (!isAdmin)
			{
				isActive = true;
				includeDeleted = false;
			}
			
			var response = await _productsServices.GetProductByIdAsync(id, isActive, includeDeleted);
			return HandleResult<ProductDetailDto>(response, nameof(GetProduct), id);
		}

		[HttpPost]
		[Authorize(Roles = "Admin")]
		public async Task<ActionResult<ApiResponse<ProductDto>>> CreateProduct(CreateProductDto model)
		{
			_logger.LogInformation($"Executing {nameof(CreateProduct)}");
			if (!ModelState.IsValid)
			{
				var errors = string.Join(", ", ModelState.Values
					.SelectMany(v => v.Errors)
					.Select(e => e.ErrorMessage)
					.ToList());
				_logger.LogError($"Validation Errors: {errors}");
				return BadRequest(ApiResponse<ProductDto>.CreateErrorResponse("Check on data", new ErrorResponse("Invalid data", errors)));
			}
			var userId = HttpContext.Items["UserId"]?.ToString();
			var response = await _productsServices.CreateProductAsync(model, userId);
			return HandleResult<ProductDto>(response, nameof(CreateProduct), response.Data?.Id);
		}
		[HttpPut("{id}")]
		[Authorize(Roles = "Admin")]
		public async Task<ActionResult<ApiResponse<ProductDto>>> UpdateProduct(int id, UpdateProductDto model)
		{
			_logger.LogInformation($"Executing {nameof(UpdateProduct)} for ID: {id}");
			if (!ModelState.IsValid)
			{
				var errors = string.Join(", ", ModelState.Values
					.SelectMany(v => v.Errors)
					.Select(e => e.ErrorMessage)
					.ToList());
				_logger.LogError($"Validation Errors: {errors}");
				return BadRequest(ApiResponse<ProductDto>.CreateErrorResponse("", new ErrorResponse("Invalid data", errors)));
			}
			var userId = HttpContext.Items["UserId"]?.ToString();
			var response = await _productsServices.UpdateProductAsync(id, model, userId);
			return HandleResult<ProductDto>(response, nameof(UpdateProduct), id);
		}

		[HttpDelete("{id}")]
		[ActionName(nameof(DeleteProduct))]
		[Authorize(Roles = "Admin")]
		public async Task<ActionResult<ApiResponse<bool>>> DeleteProduct(int id)
		{
			_logger.LogInformation($"Executing {nameof(DeleteProduct)} for ID: {id}");
			var userId = HttpContext.Items["UserId"]?.ToString();
			var response = await _productsServices.DeleteProductAsync(id, userId);
			return HandleResult<bool>(response, nameof(DeleteProduct), id);
		}

		

		[HttpPatch("{id}/restore")]
		[Authorize(Roles = "Admin")]
		public async Task<ActionResult<ApiResponse<bool>>> RestoreProductAsync(int id)
		{
			if (!ModelState.IsValid)
			{
				var errors = string.Join(", ", ModelState.Values
					.SelectMany(v => v.Errors)
					.Select(e => e.ErrorMessage)
					.ToList());
				_logger.LogError($"Validation Errors: {errors}");
				return BadRequest(ApiResponse<ProductDto>.CreateErrorResponse("", new ErrorResponse("Invalid data", errors)));
			}
			var userId = HttpContext.Items["UserId"]?.ToString();
			var result = await _productsServices.RestoreProductAsync(id, userId);
			return HandleResult(result, nameof(RestoreProductAsync), id);
		}


		[HttpGet("{id}/images")]
		public async Task<ActionResult<ApiResponse<List<ImageDto>>>> GetProductImages(int id)
		{
			var response = await _productsServices.GetProductImagesAsync(id);
			return HandleResult(response, nameof(GetProductImages), id);
		}

		[HttpPost("{id}/images")]
		[Authorize(Roles = "Admin")]
		public async Task<ActionResult<ApiResponse<List<ImageDto>>>> AddProductImages(int id, [FromForm] List<IFormFile> images)
		{
			if (!ModelState.IsValid || images == null || !images.Any())
			{
				var errors = string.Join(", ", ModelState.Values
					.SelectMany(v => v.Errors)
					.Select(e => e.ErrorMessage)
					.ToList());
				_logger.LogError($"Validation Errors: {errors}");
				return BadRequest(ApiResponse<List<ImageDto>>.CreateErrorResponse("Invalid image data", new ErrorResponse("Invalid data", errors ?? "No images provided")));
			}
			var userId = HttpContext.Items["UserId"]?.ToString();
			var response = await _productsServices.AddProductImagesAsync(id, images, userId);
			return HandleResult(response, nameof(AddProductImages), id);
		}

		[HttpDelete("{id}/images/{imageId}")]
		[Authorize(Roles = "Admin")]
		public async Task<ActionResult<ApiResponse<bool>>> RemoveProductImage(int id, int imageId)
		{
			var userId = HttpContext.Items["UserId"]?.ToString();
			var response = await _productsServices.RemoveProductImageAsync(id, imageId, userId);
			return HandleResult(response, nameof(RemoveProductImage), id);
		}

		[HttpPost("{id}/main-image")]
		[Authorize(Roles = "Admin")]
		public async Task<ActionResult<ApiResponse<ImageDto>>> UploadAndSetMainImage(int id, [FromForm] CreateImageDto mainImage)
		{
			if (!ModelState.IsValid || mainImage?.Files == null || !mainImage.Files.Any())
			{
				var errors = string.Join(", ", ModelState.Values
					.SelectMany(v => v.Errors)
					.Select(e => e.ErrorMessage)
					.ToList());
				_logger.LogError($"Validation Errors: {errors}");
				return BadRequest(ApiResponse<bool>.CreateErrorResponse("Invalid image data", new ErrorResponse("Invalid data", errors ?? "No main image provided")));
			}

			var userId = HttpContext.Items["UserId"]?.ToString();
			var response = await _productsServices.UploadAndSetMainImageAsync(id, mainImage.Files.First(), userId);
			return HandleResult(response, nameof(UploadAndSetMainImage), id);
		}

		// Removed duplicate variants endpoint - now handled by ProductVariantController


		[HttpGet]
		[AllowAnonymous]
		public async Task<ActionResult<ApiResponse<List<ProductDto>>>> GetProducts(
			[FromQuery] bool? isActive = null,
			[FromQuery] bool? includeDeleted = null,
			[FromQuery] int page = 1,
			[FromQuery] int pageSize = 10)
		{
			bool isAdmin = User?.IsInRole("Admin") == true;
			
			// For non-admin users, restrict to active and non-deleted products
			if (!isAdmin)
			{
				isActive = true;
				includeDeleted = false;
			}
			
			var response = await _productsServices.AdvancedSearchAsync(
				new AdvancedSearchDto(), page, pageSize, isActive, includeDeleted);
			return HandleResult(response, nameof(GetProducts));
		}

		[HttpGet("subcategory/{subCategoryId}")]
		[AllowAnonymous]
		public async Task<ActionResult<ApiResponse<List<ProductDto>>>> GetProductsBySubCategory(
			int subCategoryId,
			[FromQuery] bool? isActive = null,
			[FromQuery] bool? includeDeleted = null,
			[FromQuery] int page = 1,
			[FromQuery] int pageSize = 10)
		{
			bool isAdmin = User?.IsInRole("Admin") == true;
			
			// For non-admin users, restrict to active and non-deleted products
			if (!isAdmin)
			{
				isActive = true;
				includeDeleted = false;
			}
			
			var response = await _productsServices.AdvancedSearchAsync(
				new AdvancedSearchDto { Subcategoryid = subCategoryId }, page, pageSize, isActive, includeDeleted);
			return HandleResult(response, nameof(GetProductsBySubCategory), subCategoryId);
		}

		[HttpGet("bestsellers")]
		[AllowAnonymous]
		public async Task<ActionResult<ApiResponse<List<ProductDto>>>> GetBestSellers(
			[FromQuery] int page = 1,
			[FromQuery] int pageSize = 10,
			[FromQuery] bool? isActive = null,
			[FromQuery] bool? includeDeleted = null)
		{
			bool isAdmin = User?.IsInRole("Admin") == true;
			
			// For non-admin users, restrict to active and non-deleted products
			if (!isAdmin)
			{
				isActive = true;
				includeDeleted = false;
			}
			
			var response = await _productsServices.GetBestSellersAsync(page, pageSize, isActive, includeDeleted);
			return HandleResult(response, nameof(GetBestSellers));
		}

		[HttpGet("newarrivals")]
		[AllowAnonymous]
		public async Task<ActionResult<ApiResponse<List<ProductDto>>>> GetNewArrivals(
			[FromQuery] int page = 1,
			[FromQuery] int pageSize = 10,
			[FromQuery] bool? isActive = null,
			[FromQuery] bool? includeDeleted = null)
		{
			bool isAdmin = User?.IsInRole("Admin") == true;
			
			// For non-admin users, restrict to active and non-deleted products
			if (!isAdmin)
			{
				isActive = true;
				includeDeleted = false;
			}
			
			var response = await _productsServices.GetNewArrivalsAsync(page, pageSize, isActive, includeDeleted);
			return HandleResult(response, nameof(GetNewArrivals));
		}
		[HttpPatch("{id}/activate")]
		[Authorize(Roles = "Admin")]
		public async Task<ActionResult<ApiResponse<bool>>> ActiveProduct(int id)
		{
			string userId = HttpContext.Items["UserId"]?.ToString();
			var response = await _productsServices.ActivateProductAsync(id, userId);
			return HandleResult(response, nameof(ActiveProduct), id);
		}

		[HttpPatch("{id}/deactivate")]
		[Authorize(Roles = "Admin")]
		public async Task<ActionResult<ApiResponse<bool>>> DeActiveProduct(int id)
		{
			string userId = HttpContext.Items["UserId"]?.ToString();
			var response = await _productsServices.DeactivateProductAsync(id, userId);
			return HandleResult(response, nameof(DeActiveProduct), id);
		}



		[HttpPost("advanced-search")]
		[AllowAnonymous]
		public async Task<ActionResult<ApiResponse<List<ProductDto>>>> AdvancedSearch(
			[FromBody] AdvancedSearchDto searchDto,
			[FromQuery] int page = 1,
			[FromQuery] int pageSize = 10,
			[FromQuery] bool? isActive = null,
			[FromQuery] bool? includeDeleted = null)
		{
			if (!ModelState.IsValid)
			{
				var errors = string.Join(", ", ModelState.Values
					.SelectMany(v => v.Errors)
					.Select(e => e.ErrorMessage)
					.ToList());
				_logger.LogError($"Validation Errors: {errors}");
				return BadRequest(ApiResponse<List<ProductDto>>.CreateErrorResponse("Invalid search criteria", new ErrorResponse("Invalid data", errors)));
			}
			
			bool isAdmin = User?.IsInRole("Admin") == true;
			
			// For non-admin users, restrict to active and non-deleted products
			if (!isAdmin)
			{
				isActive = true;
				includeDeleted = false;
			}
			
			var response = await _productsServices.AdvancedSearchAsync(searchDto, page, pageSize, isActive, includeDeleted);
			return HandleResult(response, nameof(AdvancedSearch));
		}
	}
}