using ApplicationLayer.DtoModels.PaymentDtos;
using ApplicationLayer.DtoModels.Responses;
using ApplicationLayer.ErrorHnadling;
using ApplicationLayer.Services.PaymentServices;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace DomainLayer.Controllers
{
	[ApiController]
	[Route("api/[controller]")]
	public class PaymentController : BaseController
	{
		private readonly IPaymentServices _paymentServices;
		private readonly ILogger<PaymentController> _logger;

		public PaymentController(IPaymentServices paymentServices, ILogger<PaymentController> logger)
		{
			_paymentServices = paymentServices;
			_logger = logger;
		}

		/// <summary>
		/// Create payment for order (RESTful)
		/// POST /api/payment
		/// </summary>
		[HttpPost]
		[Authorize(Roles = "User,Admin,SuperAdmin")]
		public async Task<ActionResult<ApiResponse<PaymentResponseDto>>> CreatePayment([FromBody] CreatePaymentRequestDto paymentRequest)
		{
			try
			{
				if (!ModelState.IsValid)
				{
					var errors = GetModelErrors();
					_logger.LogWarning($"ModelState errors: {string.Join(", ", errors)}");
					return BadRequest(ApiResponse<PaymentResponseDto>.CreateErrorResponse("Invalid Data", new ErrorResponse("Invalid Data", errors), 400));
				}

				_logger.LogInformation($"Executing CreatePayment for order: {paymentRequest.OrderNumber}");
				var userId = GetUserId();
				var result = await _paymentServices.CreatePaymentMethod(paymentRequest.OrderNumber, paymentRequest.PaymentDetails, userId);
				return HandleResult(result, nameof(CreatePayment),  result.Data?.Paymentid );
			}
			catch (Exception ex)
			{
				_logger.LogError($"Error in CreatePayment: {ex.Message}");
				return StatusCode(500, ApiResponse<PaymentResponseDto>.CreateErrorResponse("Server Error", new ErrorResponse("Server Error", "An error occurred while creating the payment"), 500));
			}
		}

		///// <summary>
		///// Get payment by ID (RESTful)
		///// GET /api/payment/{paymentId}
		///// </summary>
		//[HttpGet("{paymentId}")]
		//[Authorize(Roles = "Customer,Admin")]
		//public async Task<ActionResult<ApiResponse<PaymentResponseDto>>> GetPayment(int paymentId)
		//{
		//	try
		//	{
		//		_logger.LogInformation($"Executing GetPayment for ID: {paymentId}");
		//		var userId = GetUserId();
		//		var result = await _paymentServices.GetPaymentByIdAsync(paymentId, userId);
		//		return HandleResult(result);
		//	}
		//	catch (Exception ex)
		//	{
		//		_logger.LogError($"Error in GetPayment: {ex.Message}");
		//		return StatusCode(500, ApiResponse<PaymentResponseDto>.CreateErrorResponse("Server Error", new ErrorResponse("Server Error", "An error occurred while retrieving the payment"), 500));
		//	}
		//}

		///// <summary>
		///// Get payments with filtering (RESTful)
		///// GET /api/payment?orderId={orderId}&userId={userId}&status={status}
		///// </summary>
		//[HttpGet]
		//[Authorize(Roles = "Customer,Admin")]
		//public async Task<ActionResult<ApiResponse<List<PaymentResponseDto>>>> GetPayments(
		//	[FromQuery] string? orderId = null,
		//	[FromQuery] string? userId = null,
		//	[FromQuery] string? status = null,
		//	[FromQuery] int page = 1,
		//	[FromQuery] int pageSize = 10)
		//{
		//	try
		//	{
		//		if (page <= 0 || pageSize <= 0)
		//		{
		//			return BadRequest(ApiResponse<List<PaymentResponseDto>>.CreateErrorResponse("Invalid pagination", new ErrorResponse("Invalid Data", "Page and pageSize must be greater than 0"), 400));
		//		}

		//		var role = GetUserRole();
		//		var effectiveUserId = role == "Admin" ? userId : GetUserId();

		//		_logger.LogInformation($"Executing GetPayments: orderId: {orderId}, userId: {effectiveUserId}, status: {status}");
		//		var result = await _paymentServices.GetPaymentsAsync(orderId, effectiveUserId, status, page, pageSize);
		//		return HandleResult(result);
		//	}
		//	catch (Exception ex)
		//	{
		//		_logger.LogError($"Error in GetPayments: {ex.Message}");
		//		return StatusCode(500, ApiResponse<List<PaymentResponseDto>>.CreateErrorResponse("Server Error", new ErrorResponse("Server Error", "An error occurred while retrieving payments"), 500));
		//	}
		//}

		///// <summary>
		///// Update payment status (RESTful)
		///// PUT /api/payment/{paymentId}/status
		///// </summary>
		//[HttpPut("{paymentId}/status")]
		//[Authorize(Roles = "Admin")]
		//public async Task<ActionResult<ApiResponse<PaymentResponseDto>>> UpdatePaymentStatus(string paymentId, [FromBody] UpdatePaymentStatusRequest request)
		//{
		//	try
		//	{
		//		if (!ModelState.IsValid)
		//		{
		//			var errors = GetModelErrors();
		//			_logger.LogWarning($"ModelState errors: {string.Join(", ", errors)}");
		//			return BadRequest(ApiResponse<PaymentResponseDto>.CreateErrorResponse("Invalid Data", new ErrorResponse("Invalid Data", errors), 400));
		//		}

		//		_logger.LogInformation($"Executing UpdatePaymentStatus for ID: {paymentId}");
		//		var userId = GetUserId();
		//		var result = await _paymentServices.UpdatePaymentStatusAsync(paymentId, request.Status, userId);
		//		return HandleResult(result);
		//	}
		//	catch (Exception ex)
		//	{
		//		_logger.LogError($"Error in UpdatePaymentStatus: {ex.Message}");
		//		return StatusCode(500, ApiResponse<PaymentResponseDto>.CreateErrorResponse("Server Error", new ErrorResponse("Server Error", "An error occurred while updating payment status"), 500));
		//	}
		//}

		#region Helper Methods

		private string GetUserId()
		{
			return User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? string.Empty;
		}

		private string GetUserRole()
		{
			return User.FindFirst(ClaimTypes.Role)?.Value ?? "User";
		}

	

	
		#endregion
	}
}

// Supporting DTOs for the PaymentController
public class CreatePaymentRequestDto
{
	public string OrderNumber { get; set; } = string.Empty;
	public CreatePaymentOfCustomer PaymentDetails { get; set; } = new();
}
