using ApplicationLayer.DtoModels.DiscoutDtos;
using ApplicationLayer.DtoModels.Responses;
using ApplicationLayer.ErrorHnadling;
using ApplicationLayer.Services.DiscountServices;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace DomainLayer.Controllers
{
	[Route("api/[controller]")]
	[ApiController]
	[Authorize(Roles = "Admin,SuperAdmin")]
	public class DiscountController : BaseController
	{
		private readonly IDiscountService _discountService;
		private readonly ILogger<DiscountController> _logger;

		public DiscountController(IDiscountService discountService, ILogger<DiscountController> logger)
		{
			_discountService = discountService;
			_logger = logger;
		}

		
	
		[HttpGet("{id}")]
		[ActionName(nameof(GetByIdAsync))]
		[ResponseCache(Duration = 60, VaryByQueryKeys = new string[] { "id", "isActive", "includeDeleted" })]
		public async Task<ActionResult<ApiResponse<DiscountDto>>> GetByIdAsync(
			int id,
			[FromQuery] bool? isActive = null,
			[FromQuery] bool includeDeleted = false)
		{
			if (id <= 0)
			{
				return BadRequest(ApiResponse<DiscountDto>.CreateErrorResponse(
					"Invalid ID",
					new ErrorResponse("Validation", new List<string> { "ID must be greater than 0" }),
					400
				));
			}
			_logger.LogInformation($"Executing {nameof(GetByIdAsync)} for id: {id}, isActive: {isActive}, includeDeleted: {includeDeleted}");

			var response = await _discountService.GetDiscountByIdAsync(id, isActive, includeDeleted);
			return HandleResult<DiscountDto>(response, nameof(GetByIdAsync), id);
		}

		[HttpPost]
		[ActionName(nameof(CreateAsync))]
		public async Task<ActionResult<ApiResponse<DiscountDto>>> CreateAsync(CreateDiscountDto model)
		{
			_logger.LogInformation($"Executing {nameof(CreateAsync)}");
			if (!ModelState.IsValid)
			{
				var errors = string.Join(", ", ModelState.Values
					.SelectMany(v => v.Errors)
					.Select(e => e.ErrorMessage)
					.ToList());
				_logger.LogError($"Validation Errors: {errors}");
				return BadRequest(ApiResponse<DiscountDto>.CreateErrorResponse("Check on data", new ErrorResponse("Invalid data", errors)));
			}

			var userId =  GetUserId();
			var response = await _discountService.CreateDiscountAsync(model, userId);
			return HandleResult<DiscountDto>(response, nameof(GetByIdAsync), response.Data?.Id);
		}

		[HttpPut("{id}")]
		[ActionName(nameof(UpdateAsync))]
		public async Task<ActionResult<ApiResponse<DiscountDto>>> UpdateAsync(int id, UpdateDiscountDto model)
		{
			_logger.LogInformation($"Executing {nameof(UpdateAsync)} for ID: {id}");
			if (!ModelState.IsValid)
			{
				var errors = GetModelErrors();
				_logger.LogError($"Validation Errors: {errors}");
				return BadRequest(ApiResponse<DiscountDto>.CreateErrorResponse("", new ErrorResponse("Invalid data", errors)));
			}

			var userId = GetUserId();
			var response = await _discountService.UpdateDiscountAsync(id, model, userId);
			return HandleResult<DiscountDto>(response, nameof(GetByIdAsync), id);
		}

		[HttpDelete("{id}")]
		[ActionName(nameof(DeleteAsync))]
		public async Task<ActionResult<ApiResponse<bool>>> DeleteAsync(int id)
		{
			if (id <= 0)
			{
				return BadRequest(ApiResponse<bool>.CreateErrorResponse(
					"Invalid ID",
					new ErrorResponse("Validation", new List<string> { "ID must be greater than 0" }),
					400
				));
			}
			_logger.LogInformation($"Executing {nameof(DeleteAsync)} for ID: {id}");
			var userId =  GetUserId();
			var response = await _discountService.DeleteDiscountAsync(id, userId);
			return HandleResult<bool>(response, nameof(GetByIdAsync), id);
		}

		[HttpPatch("{id}/restore")]
		[ActionName(nameof(RestoreAsync))]
		public async Task<ActionResult<ApiResponse<DiscountDto>>> RestoreAsync(int id)
		{
			if (id <= 0)
			{
				return BadRequest(ApiResponse<DiscountDto>.CreateErrorResponse(
					"Invalid ID",
					new ErrorResponse("Validation", new List<string> { "ID must be greater than 0" }),
					400
				));
			}
			_logger.LogInformation($"Executing {nameof(RestoreAsync)} for ID: {id}");
			var userId =  GetUserId();
			var response = await _discountService.RestoreDiscountAsync(id, userId);
			return HandleResult<DiscountDto>(response, nameof(GetByIdAsync), id);
		}

		[HttpGet()]
		[ActionName(nameof(FilterAsync))]
		[ResponseCache(Duration = 60, VaryByQueryKeys = new string[] { "search", "isActive", "includeDeleted", "page", "pageSize" })]
		public async Task<ActionResult<ApiResponse<List<DiscountDto>>>> FilterAsync(
			[FromQuery] string? search,
			[FromQuery] bool? isActive,
			[FromQuery] bool includeDeleted = false,
			[FromQuery] int page = 1,
			[FromQuery] int pageSize = 10)
		{
			_logger.LogInformation($"Executing {nameof(FilterAsync)} with filters");

			if (page <= 0 || pageSize <= 0)
			{
				return BadRequest(ApiResponse<List<DiscountDto>>.CreateErrorResponse(
					"Invalid Pagination",
					new ErrorResponse("Validation", new List<string> { "Page and PageSize must be greater than 0" }),
					400
				));
			}

			var role = HasManagementRole();
			var response = await _discountService.FilterAsync(search, isActive, includeDeleted, page, pageSize, role);
			return HandleResult<List<DiscountDto>>(response, nameof(FilterAsync));
		}

	

		[HttpGet("expired")]
		[ActionName(nameof(GetExpiredAsync))]
		[ResponseCache(Duration = 60)]
		public async Task<ActionResult<ApiResponse<List<DiscountDto>>>> GetExpiredAsync([FromQuery]int page = 1,[FromQuery] int pagesize = 10)
		{
			_logger.LogInformation($"Executing {nameof(GetExpiredAsync)}");
			
			var response = await _discountService.GetExpiredDiscountsAsync(page,pagesize);
			return HandleResult<List<DiscountDto>>(response, nameof(GetExpiredAsync));
		}

		[HttpGet("upcoming")]
		[ActionName(nameof(GetUpcomingAsync))]
		[ResponseCache(Duration = 60)]
		public async Task<ActionResult<ApiResponse<List<DiscountDto>>>> GetUpcomingAsync([FromQuery] int page = 1, [FromQuery] int pagesize = 10)
		{
			_logger.LogInformation($"Executing {nameof(GetUpcomingAsync)}");

			var response = await _discountService.GetUpcomingDiscountsAsync(page,pagesize);
			return HandleResult<List<DiscountDto>>(response, nameof(GetUpcomingAsync));
		}

		[HttpGet("category/{categoryId}")]
		[ActionName(nameof(GetByCategoryAsync))]
		[ResponseCache(Duration = 60, VaryByQueryKeys = new string[] { "categoryId" })]
		public async Task<ActionResult<ApiResponse<List<DiscountDto>>>> GetByCategoryAsync(int categoryId)
		{
			if (categoryId <= 0)
			{
				return BadRequest(ApiResponse<List<DiscountDto>>.CreateErrorResponse(
					"Invalid Category ID",
					new ErrorResponse("Validation", new List<string> { "Category ID must be greater than 0" }),
					400
				));
			}
			_logger.LogInformation($"Executing {nameof(GetByCategoryAsync)} for categoryId: {categoryId}");

			var response = await _discountService.GetDiscountsByCategoryAsync(categoryId);
			return HandleResult<List<DiscountDto>>(response, nameof(GetByCategoryAsync), categoryId);
		}

		[HttpPatch("{id}/activate")]
		[ActionName(nameof(ActivateAsync))]
		public async Task<ActionResult<ApiResponse<bool>>> ActivateAsync(int id)
		{
			if (id <= 0)
			{
				return BadRequest(ApiResponse<bool>.CreateErrorResponse(
					"Invalid ID",
					new ErrorResponse("Validation", new List<string> { "ID must be greater than 0" }),
					400
				));
			}
			_logger.LogInformation($"Executing {nameof(ActivateAsync)} for ID: {id}");
			var userId =  GetUserId();
			var response = await _discountService.ActivateDiscountAsync(id, userId);
			return HandleResult<bool>(response, nameof(ActivateAsync), id);
		}

		[HttpPatch("{id}/deactivate")]
		[ActionName(nameof(DeactivateAsync))]
		public async Task<ActionResult<ApiResponse<bool>>> DeactivateAsync(int id)
		{
			if (id <= 0)
			{
				return BadRequest(ApiResponse<bool>.CreateErrorResponse(
					"Invalid ID",
					new ErrorResponse("Validation", new List<string> { "ID must be greater than 0" }),
					400
				));
			}
			_logger.LogInformation($"Executing {nameof(DeactivateAsync)} for ID: {id}");
			var userId =  GetUserId();
			var response = await _discountService.DeactivateDiscountAsync(id, userId);
			return HandleResult<bool>(response, nameof(DeactivateAsync), id);
		}

	}
}
