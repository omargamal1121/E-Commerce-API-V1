using E_Commerce.DtoModels;

using E_Commerce.DtoModels.Responses;
using E_Commerce.Enums;
using E_Commerce.ErrorHnadling;

using E_Commerce.Services;
using E_Commerce.Services.PaymentMethodsServices;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace E_Commerce.Controllers
{
	[Route("api/[controller]")]
	[ApiController]
	public class PaymentMethodController : ControllerBase
	{
		private readonly IPaymentMethodsServices _paymentMethodService;
		private readonly ILogger<PaymentMethodController> _logger;

		public PaymentMethodController(ILogger<PaymentMethodController> logger,IPaymentMethodsServices paymentMethodService)
		{
			_logger = logger;
			_paymentMethodService = paymentMethodService;
		}

	
		[Authorize]
		[HttpDelete("{id}")]
		public async Task<ActionResult<ApiResponse<bool>>> RemovePaymentMethod(int id)
		{
			var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
			var result = await _paymentMethodService.RemovePaymentMethod(id, userId);
			return HandleResult(result, nameof(RemovePaymentMethod));
		}

		[Authorize]
		[HttpPost]
		public async Task<ActionResult<ApiResponse<PaymentMethodDto>>> CreatePaymentMethod([FromForm] Createpaymentmethoddto paymentDto)
		{
			var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
			var result = await _paymentMethodService.CreatePaymentMethod(paymentDto, userId);
			return HandleResult(result,nameof(CreatePaymentMethod));
		}

		[Authorize]
		[HttpPut("{id}")]
		public async Task<ActionResult<ApiResponse<bool>>> UpdatePaymentMethod(int id, [FromForm] Updatepaymentmethoddto paymentDto)
		{
			var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
			var result = await _paymentMethodService.UpdatePaymentMethod(id, paymentDto, userId);
			return HandleResult(result,nameof(UpdatePaymentMethod));
		}
		[HttpPut("Deactivate/{id}")]
		[Authorize(Roles = "Admin")]
		public async Task<ActionResult<ApiResponse<bool>>> DeactivatePaymentMethod(int id)
		{
			var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

			var result = await _paymentMethodService.DeactivatePaymentMethodAsync(id, userId);

			
			return HandleResult(result,nameof(DeactivatePaymentMethod),id);
		}
		[HttpPut("Activate/{id}")]
		[Authorize(Roles = "Admin")]
		public async Task<ActionResult<ApiResponse<bool>>> ActivatePaymentMethod(int id)
		{
			var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

			var result = await _paymentMethodService.ActivatePaymentMethodAsync(id, userId);



			return HandleResult(result, nameof(ActivatePaymentMethod), id);
		}
		[HttpGet]
		public async Task<ActionResult<ApiResponse<List<PaymentMethodDto>>>> GetAll([FromQuery] bool? isActive, [FromQuery] bool? isDeleted, [FromQuery] int page = 1, [FromQuery] int pageSize = 10)
		{
			_logger.LogInformation($"Admin requested GetAll PaymentMethods | isActive: {isActive}, isDeleted: {isDeleted}, Page: {page}, PageSize: {pageSize}");

			var result = await _paymentMethodService.GetPaymentMethodsAsync(isActive, isDeleted, page, pageSize);
			return HandleResult(result, nameof(GetAll));
		}



		// Handle method for wrapping result into standard ApiResponse
		private ActionResult<ApiResponse<T>> HandleResult<T>(Result<T> result, string apiname, int? id = null)
		{
		
			ApiResponse<T> apiResponse;
			if (result.Success)
			{
				apiResponse = ApiResponse<T>.CreateSuccessResponse(result.Message, result.Data, result.StatusCode, warnings: result.Warnings);
			}
			else
			{
				var errorResponse = (result.Warnings != null && result.Warnings.Count > 0)
					? new ErrorResponse("Error", result.Warnings)
					: new ErrorResponse("Error", result.Message);
				apiResponse = ApiResponse<T>.CreateErrorResponse(result.Message, errorResponse, result.StatusCode, warnings: result.Warnings);
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

	}
}
