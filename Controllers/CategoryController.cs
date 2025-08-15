using AutoMapper;
using E_Commerce.DtoModels;
using E_Commerce.DtoModels.CategoryDtos;
using E_Commerce.DtoModels.ImagesDtos;
using E_Commerce.DtoModels.Responses;
using E_Commerce.DtoModels.Shared;
using E_Commerce.ErrorHnadling;
using E_Commerce.Interfaces;
using E_Commerce.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace E_Commerce.Controllers
{
	[Route("api/categories")]
	[ApiController]
	public class CategoriesController : ControllerBase
	{
		private readonly ICategoryServices _categoryServices;
		private readonly ILogger<CategoriesController> _logger;
		private readonly ICategoryLinkBuilder _linkBuilder;

		public CategoriesController(
			ICategoryLinkBuilder linkBuilder,
			ICategoryServices categoryServices,
			ILogger<CategoriesController> logger)
		{
			_linkBuilder = linkBuilder;
			_categoryServices = categoryServices;
			_logger = logger;
		}

		private ActionResult<ApiResponse<T>> HandleResult<T>(Result<T> result, string apiname, int? id = null)
		{
			var links = _linkBuilder.MakeRelSelf(_linkBuilder.GenerateLinks(id), apiname);
			ApiResponse<T> apiResponse;
			if (result.Success)
			{
				apiResponse = ApiResponse<T>.CreateSuccessResponse(result.Message, result.Data, result.StatusCode, warnings: result.Warnings, links: links);
			}
			else
			{
				var errorResponse = (result.Warnings != null && result.Warnings.Count > 0)
					? new ErrorResponse("Error", result.Warnings)
					: new ErrorResponse("Error", result.Message);
				apiResponse = ApiResponse<T>.CreateErrorResponse(result.Message, errorResponse, result.StatusCode, warnings: result.Warnings, links: links);
			}

			switch (result.StatusCode)
			{
				case 200: return Ok(apiResponse);
				case 201: return StatusCode(201, apiResponse);
				case 400: return BadRequest(apiResponse);
				case 401: return Unauthorized(apiResponse);
				case 409: return Conflict(apiResponse);
				default: return StatusCode(result.StatusCode, apiResponse);
			}
		}

		// RESTful endpoints
		/// <summary>
		/// Get category by ID - accessible to all users, admins can control visibility filters
		/// </summary>
		[HttpGet("{id}")]
		[AllowAnonymous]
		[ActionName(nameof(GetCategoryById))]
		public async Task<ActionResult<ApiResponse<CategorywithdataDto>>> GetCategoryById(
			int id,
			[FromQuery] bool? isActive = null,
			[FromQuery] bool? includeDeleted = null)
		{
			_logger.LogInformation($"Executing {nameof(GetCategoryById)} for id: {id}, isActive: {isActive}, includeDeleted: {includeDeleted}");
			
			// Enhanced validation
			if (id <= 0)
			{
				return BadRequest(ApiResponse<CategorywithdataDto>.CreateErrorResponse("Invalid category ID", new ErrorResponse("Validation", new List<string> { "Category ID must be greater than 0" }), 400));
			}
			
			// Role-based filtering
			bool isAdmin = User?.IsInRole("Admin") ?? false;
			
			// Regular users can only see active, non-deleted categories
			if (!isAdmin)
			{
				isActive = true;
				includeDeleted = false;
			}
			
			// Admins can use any filters, regular users get fixed filters
			var result = await _categoryServices.GetCategoryByIdAsync(id, isActive, includeDeleted);
			return HandleResult(result, "GetCategoryById", id);
		}

		/// <summary>
		/// Get all categories - accessible to all users (only active, non-deleted)
		/// </summary>
		[AllowAnonymous]
		[HttpGet]
		[ActionName(nameof(GetCategories))]
		public async Task<ActionResult<ApiResponse<List<CategoryDto>>>> GetCategories(
			[FromQuery] string? search = null,
			[FromQuery] bool? isActive = null,
			[FromQuery] bool? isDeleted = null,
			[FromQuery] int page = 1,
			[FromQuery] int pageSize = 10)
		{
			_logger.LogInformation($"Executing {nameof(GetCategories)} with search: {search}, isActive: {isActive}, isDeleted: {isDeleted}, page: {page}");
			
			// Role-based filtering
			bool isAdmin = User?.IsInRole("Admin") ?? false;
			
			// Regular users can only see active, non-deleted categories
			if (!isAdmin)
			{
				isActive = true;
				isDeleted = false;
			}
			
			// Admins can use any filters, regular users get fixed filters
			var searchResult = await _categoryServices.FilterAsync(search, isActive, isDeleted, page, pageSize);
			return HandleResult(searchResult, "GetCategories");
		}

		// Remove GetCategoriesAdmin method - functionality merged into GetCategories


		/// <summary>
		/// Create a new category
		/// </summary>
		[HttpPost]
		[Authorize(Roles = "Admin")]
		[ActionName(nameof(CreateAsync))]
		public async Task<ActionResult<ApiResponse<CategoryDto>>> CreateAsync([FromForm] CreateCategotyDto categoryDto)
		{
			_logger.LogInformation($"Executing {nameof(CreateAsync)}");
			
			// Enhanced validation to match service improvements
			if (categoryDto == null)
			{
				return BadRequest(ApiResponse<CategoryDto>.CreateErrorResponse("Category data is required", new ErrorResponse("Validation", new List<string> { "Category model cannot be null" }), 400));
			}
			
			if (!ModelState.IsValid)
			{
				var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).ToList();
				return BadRequest(ApiResponse<CategoryDto>.CreateErrorResponse("Validation failed", new ErrorResponse("Invalid Data", errors)));
			}
			
			var userId = HttpContext.Items["UserId"]?.ToString();
			if (string.IsNullOrWhiteSpace(userId))
			{
				return Unauthorized(ApiResponse<CategoryDto>.CreateErrorResponse("User authentication required", new ErrorResponse("Authentication", new List<string> { "User ID is required" }), 401));
			}
			
			var result = await _categoryServices.CreateAsync(categoryDto, userId);
			return HandleResult(result, nameof(CreateAsync));
		}

		/// <summary>
		/// Update a category
		/// </summary>
		[HttpPut("{id}")]
		[Authorize(Roles = "Admin")]
		[ActionName(nameof(UpdateAsync))]
		public async Task<ActionResult<ApiResponse<CategoryDto>>> UpdateAsync(int id, [FromForm] UpdateCategoryDto categoryDto)
		{
			_logger.LogInformation($"Executing {nameof(UpdateAsync)} for id: {id}");
			
			// Enhanced validation to match service improvements
			if (id <= 0)
			{
				return BadRequest(ApiResponse<CategoryDto>.CreateErrorResponse("Invalid category ID", new ErrorResponse("Validation", new List<string> { "Category ID must be greater than 0" }), 400));
			}
			
			if (categoryDto == null)
			{
				return BadRequest(ApiResponse<CategoryDto>.CreateErrorResponse("Category data is required", new ErrorResponse("Validation", new List<string> { "Category model cannot be null" }), 400));
			}
			
			if (!ModelState.IsValid)
			{
				var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).ToList();
				return BadRequest(ApiResponse<CategoryDto>.CreateErrorResponse("Validation failed", new ErrorResponse("Invalid Data", errors)));
			}
			
			var userId = HttpContext.Items["UserId"]?.ToString();
			if (string.IsNullOrWhiteSpace(userId))
			{
				return Unauthorized(ApiResponse<CategoryDto>.CreateErrorResponse("User authentication required", new ErrorResponse("Authentication", new List<string> { "User ID is required" }), 401));
			}
			
			var result = await _categoryServices.UpdateAsync(id, categoryDto, userId);
			return HandleResult(result, nameof(UpdateAsync), id);
		}

		/// <summary>
		/// Delete a category
		/// </summary>
		[HttpDelete("{id}")]
		[Authorize(Roles = "Admin")]
		[ActionName(nameof(DeleteAsync))]
		public async Task<ActionResult<ApiResponse<bool>>> DeleteAsync(int id)
		{
			_logger.LogInformation($"Executing {nameof(DeleteAsync)} for id: {id}");
			
			// Enhanced validation to match service improvements
			if (id <= 0)
			{
				return BadRequest(ApiResponse<bool>.CreateErrorResponse("Invalid category ID", new ErrorResponse("Validation", new List<string> { "Category ID must be greater than 0" }), 400));
				
			}
			
			var userId = HttpContext.Items["UserId"]?.ToString();
			if (string.IsNullOrWhiteSpace(userId))
			{
				return Unauthorized(ApiResponse<bool>.CreateErrorResponse("User authentication required", new ErrorResponse("Authentication", new List<string> { "User ID is required" }), 401));
			}
			
			var result = await _categoryServices.DeleteAsync(id, userId);
			return HandleResult(result, nameof(DeleteAsync));
		}

		/// <summary>
		/// Add images to a category (both main and additional images)
		/// </summary>
		[HttpPost("{id}/images")]
		[Authorize(Roles = "Admin")]
		[ActionName(nameof(AddImagesToCategoryAsync))]
		public async Task<ActionResult<ApiResponse<List<ImageDto>>>> AddImagesToCategoryAsync(int id, [FromForm] AddImagesDto images)
		{
			_logger.LogInformation($"Executing {nameof(AddImagesToCategoryAsync)} for id: {id}");
			
			// Enhanced validation
			if (id <= 0)
			{
				return BadRequest(ApiResponse<List<ImageDto>>.CreateErrorResponse("Invalid category ID", new ErrorResponse("Validation", new List<string> { "Category ID must be greater than 0" }), 400));
			}
			
			if (images == null || images.Images == null || !images.Images.Any())
			{
				return BadRequest(ApiResponse<List<ImageDto>>.CreateErrorResponse("Images are required", new ErrorResponse("Validation", new List<string> { "At least one image is required." }), 400));
			}
			
			var userId = HttpContext.Items["UserId"]?.ToString();
			if (string.IsNullOrWhiteSpace(userId))
			{
				return Unauthorized(ApiResponse<List<ImageDto>>.CreateErrorResponse("User authentication required", new ErrorResponse("Authentication", new List<string> { "User ID is required" }), 401));
			}
			
			var result = await _categoryServices.AddImagesToCategoryAsync(id, images.Images, userId);
			return HandleResult(result, nameof(AddImagesToCategoryAsync), id);
		}

		/// <summary>
		/// Add a main image to a category
		/// </summary>
		[HttpPost("{id}/images/main")]
		[Authorize(Roles = "Admin")]
		[ActionName(nameof(AddMainImageAsync))]
		public async Task<ActionResult<ApiResponse<ImageDto>>> AddMainImageAsync(int id, [FromForm] AddMainImageDto mainImage)
		{
			_logger.LogInformation($"Executing {nameof(AddMainImageAsync)} for id: {id}");
			
			// Enhanced validation
			if (id <= 0)
			{
				return BadRequest(ApiResponse<ImageDto>.CreateErrorResponse("Invalid category ID", new ErrorResponse("Validation", new List<string> { "Category ID must be greater than 0" }), 400));
			}
			
			if (mainImage == null || mainImage.Image == null || mainImage.Image.Length == 0)
			{
				return BadRequest(ApiResponse<ImageDto>.CreateErrorResponse("Image is required", new ErrorResponse("Validation", new List<string> { "Main image is required." }), 400));
			}
			
			var userId = HttpContext.Items["UserId"]?.ToString();
			if (string.IsNullOrWhiteSpace(userId))
			{
				return Unauthorized(ApiResponse<ImageDto>.CreateErrorResponse("User authentication required", new ErrorResponse("Authentication", new List<string> { "User ID is required" }), 401));
			}
			
			var result = await _categoryServices.AddMainImageToCategoryAsync(id, mainImage.Image, userId);
			return HandleResult(result, nameof(AddMainImageAsync), id);
		}

		/// <summary>
		/// Remove an image from a category
		/// </summary>
		[HttpDelete("{id}/images/{imageId}")]
		[Authorize(Roles = "Admin")]
		[ActionName(nameof(RemoveImageAsync))]
		public async Task<ActionResult<ApiResponse<bool>>> RemoveImageAsync(int id, int imageId)
		{
			_logger.LogInformation($"Executing {nameof(RemoveImageAsync)} for categoryId: {id}, imageId: {imageId}");
			
			// Enhanced validation
			if (id <= 0)
			{
				return BadRequest(ApiResponse<CategoryDto>.CreateErrorResponse("Invalid category ID", new ErrorResponse("Validation", new List<string> { "Category ID must be greater than 0" }), 400));
			}
			
			if (imageId <= 0)
			{
				return BadRequest(ApiResponse<CategoryDto>.CreateErrorResponse("Invalid image ID", new ErrorResponse("Validation", new List<string> { "Image ID must be greater than 0" }), 400));
			}
			
			var userId = HttpContext.Items["UserId"]?.ToString();
			if (string.IsNullOrWhiteSpace(userId))
			{
				return Unauthorized(ApiResponse<CategoryDto>.CreateErrorResponse("User authentication required", new ErrorResponse("Authentication", new List<string> { "User ID is required" }), 401));
			}
			
			var result = await _categoryServices.RemoveImageFromCategoryAsync(id, imageId, userId);
			return HandleResult(result, nameof(RemoveImageAsync), id);
		}

		/// <summary>
		/// Activate a category
		/// </summary>
		[HttpPatch("{id}/activate")]
		[Authorize(Roles = "Admin")]
		[ActionName(nameof(ActivateCategoryAsync))]
		public async Task<ActionResult<ApiResponse<bool>>> ActivateCategoryAsync(int id)
		{
			_logger.LogInformation($"Executing {nameof(ActivateCategoryAsync)} for id: {id}");
			
			// Enhanced validation
			if (id <= 0)
			{
				return BadRequest(ApiResponse<bool>.CreateErrorResponse("Invalid category ID", new ErrorResponse("Validation", new List<string> { "Category ID must be greater than 0" }), 400));
			}
			
			var userId = HttpContext.Items["UserId"]?.ToString();
			if (string.IsNullOrWhiteSpace(userId))
			{
				return Unauthorized(ApiResponse<bool>.CreateErrorResponse("User authentication required", new ErrorResponse("Authentication", new List<string> { "User ID is required" }), 401));
			}
			
			var result = await _categoryServices.ActivateCategoryAsync(id, userId);
			return HandleResult(result, nameof(ActivateCategoryAsync), id);
		}

		/// <summary>
		/// Deactivate a category
		/// </summary>
		[HttpPatch("{id}/deactivate")]
		[Authorize(Roles = "Admin")]
		[ActionName(nameof(DeactivateCategoryAsync))]
		public async Task<ActionResult<ApiResponse<bool>>> DeactivateCategoryAsync(int id)
		{
			_logger.LogInformation($"Executing {nameof(DeactivateCategoryAsync)} for id: {id}");
			
			// Enhanced validation
			if (id <= 0)
			{
				return BadRequest(ApiResponse<bool>.CreateErrorResponse("Invalid category ID", new ErrorResponse("Validation", new List<string> { "Category ID must be greater than 0" }), 400));
			}
			
			var userId = HttpContext.Items["UserId"]?.ToString();
			if (string.IsNullOrWhiteSpace(userId))
			{
				return Unauthorized(ApiResponse<bool>.CreateErrorResponse("User authentication required", new ErrorResponse("Authentication", new List<string> { "User ID is required" }), 401));
			}
			
			var result = await _categoryServices.DeactivateCategoryAsync(id, userId);
			return HandleResult(result, nameof(DeactivateCategoryAsync), id);
		}

		/// <summary>
		/// Restore a deleted category
		/// </summary>
		[HttpPatch("{id}/restore")]
		[Authorize(Roles = "Admin")]
		[ActionName(nameof(RestoreCategoryAsync))]
		public async Task<ActionResult<ApiResponse<CategoryDto>>> RestoreCategoryAsync(int id)
		{
			_logger.LogInformation($"Executing {nameof(RestoreCategoryAsync)} for id: {id}");
			
			// Enhanced validation
			if (id <= 0)
			{
				return BadRequest(ApiResponse<CategoryDto>.CreateErrorResponse("Invalid category ID", new ErrorResponse("Validation", new List<string> { "Category ID must be greater than 0" }), 400));
			}
			
			var userId = HttpContext.Items["UserId"]?.ToString();
			if (string.IsNullOrWhiteSpace(userId))
			{
				return Unauthorized(ApiResponse<CategoryDto>.CreateErrorResponse("User authentication required", new ErrorResponse("Authentication", new List<string> { "User ID is required" }), 401));
			}
			
			var result = await _categoryServices.ReturnRemovedCategoryAsync(id, userId);
			return HandleResult(result, nameof(RestoreCategoryAsync), id);
		}

	}
}
