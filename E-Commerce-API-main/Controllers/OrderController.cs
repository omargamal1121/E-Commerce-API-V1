using ApplicationLayer.DtoModels.OrderDtos;
using ApplicationLayer.DtoModels.Responses;
using ApplicationLayer.ErrorHnadling;
using ApplicationLayer.Interfaces;
using ApplicationLayer.Services;
using DomainLayer.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace DomainLayer.Controllers
{
	[ApiController]
	[Route("api/[controller]")]
	public class OrderController : BaseController
	{
		private readonly IOrderServices _orderServices;
		private readonly ILogger<OrderController> _logger;

		public OrderController(IOrderServices orderServices, ILogger<OrderController> logger):base(null)
		{
			_orderServices = orderServices;
			_logger = logger;
		}

		#region RESTful CRUD Operations

		/// <summary>
		/// Get all orders with filtering and pagination (RESTful)
		/// GET /api/order?userId={userId}&deleted={deleted}&page={page}&pageSize={pageSize}&status={status}
		/// - Admins can filter by userId and see all orders
		/// - Customers automatically filter to their own orders
		/// </summary>
		[HttpGet]
		[Authorize(Roles = "User,Admin,SuperAdmin,DeliveryCompany")]
		public async Task<ActionResult<ApiResponse<List<OrderListDto>>>> GetOrders(
			[FromQuery] string? userId = null,
			[FromQuery] bool? deleted = null,
			[FromQuery] int page = 1,
			[FromQuery] int pageSize = 10,
			[FromQuery] OrderStatus? status = null)
		{
			try
			{
				if (!ModelState.IsValid)
				{
					var errors = GetModelErrors();
					_logger.LogWarning($"ModelState errors: {string.Join(", ", errors)}");
					return BadRequest(ApiResponse<List<OrderListDto>>.CreateErrorResponse("Invalid Data", new ErrorResponse("Invalid Data", errors), 400));
				}

				if (page <= 0 || pageSize <= 0)
				{
					return BadRequest(ApiResponse<List<OrderListDto>>.CreateErrorResponse("Invalid pagination", new ErrorResponse("Invalid Data", "Page and pageSize must be greater than 0"), 400));
				}

				var role = HasManagementRole();
				string? effectiveUserId = userId;


                if (!role)
					 effectiveUserId = GetUserId();

				_logger.LogInformation($"Executing GetOrders: role: {role}, userId: {effectiveUserId}, deleted: {deleted}, page: {page}, size: {pageSize}, status: {status}");
				var result = await _orderServices.FilterOrdersAsync(effectiveUserId, deleted, page, pageSize, status,role);
				return HandleResult(result);
			}
			catch (Exception ex)
			{
				_logger.LogError($"Error in GetOrders: {ex.Message}");
				return StatusCode(500, ApiResponse<List<OrderListDto>>.CreateErrorResponse("Server Error", new ErrorResponse("Server Error", "An error occurred while retrieving orders"), 500));
			}
		}

		/// <summary>
		/// Get order by ID (RESTful)
		/// GET /api/order/{orderId}
		/// - Customers can only access their own orders
		/// - Admins can access any order
		/// </summary>
		[HttpGet("{orderId}")]
		[Authorize(Roles = "User,Admin,SuperAdmin,DeliveryCompany")]
		public async Task<ActionResult<ApiResponse<OrderDto>>> GetOrder(int orderId)
		{
			try
			{
				_logger.LogInformation($"Executing GetOrder for ID: {orderId}");
				var userId = GetUserId();
                var isadmin = HasManagementRole();
                var result = await _orderServices.GetOrderByIdAsync(orderId, userId,isadmin);
				return HandleResult(result);
			}
			catch (Exception ex)
			{
				_logger.LogError($"Error in GetOrder: {ex.Message}");
				return StatusCode(500, ApiResponse<OrderDto>.CreateErrorResponse("Server Error", new ErrorResponse("Server Error", "An error occurred while retrieving the order"), 500));
			}
		}

		/// <summary>
		/// Create new order (RESTful)
		/// POST /api/order
		/// - Creates order from customer's cart
		/// </summary>
		[HttpPost]
		[Authorize(Roles = "User,Admin,SuperAdmin,DeliveryCompany")]
		public async Task<ActionResult<ApiResponse<OrderAfterCreatedto>>> CreateOrder([FromBody] CreateOrderDto orderDto)
		{
			try
			{
				if (!ModelState.IsValid)
				{
					var errors = GetModelErrors();
					_logger.LogWarning($"ModelState errors: {string.Join(", ", errors)}");
					return BadRequest(ApiResponse<OrderAfterCreatedto>.CreateErrorResponse("Invalid Data", new ErrorResponse("Invalid Data", errors), 400));
				}

				_logger.LogInformation("Executing CreateOrder");
				var userId = GetUserId();
				var result = await _orderServices.CreateOrderFromCartAsync(userId, orderDto);
				return HandleResult(result, nameof(CreateOrder) );
			}
			catch (Exception ex)
			{
				_logger.LogError($"Error in CreateOrder: {ex.Message}");
				return StatusCode(500, ApiResponse<OrderAfterCreatedto>.CreateErrorResponse("Server Error", new ErrorResponse("Server Error", "An error occurred while creating the order"), 500));
			}
		}

		/// <summary>
		/// Update order status (RESTful)
		/// PUT /api/order/{orderId}/status?status={status}
		/// - Admins can set any supported status
		/// - Customers can only cancel their own orders
		/// </summary>
		[HttpPut("{orderId}/status")]
		[Authorize(Roles = "User,Admin,SuperAdmin,DeliveryCompany")]
		public async Task<ActionResult<ApiResponse<bool>>> UpdateOrderStatus(int orderId, [FromBody] OrderStatusNoteDto body, [FromQuery] OrderStatus status)
		{
			try
			{
				var actorId = GetUserId();

				if (HasManagementRole())
				{
					Result<bool> result = status switch
					{
						OrderStatus.Confirmed => await _orderServices.ConfirmOrderAsync(orderId, actorId,false,true, body?.Notes),
						OrderStatus.Processing => await _orderServices.ProcessOrderAsync(orderId, actorId, body?.Notes),
						OrderStatus.Shipped => await _orderServices.ShipOrderAsync(orderId, actorId),
						OrderStatus.Delivered => await _orderServices.DeliverOrderAsync(orderId, actorId),
						OrderStatus.Complete => await _orderServices.CompleteOrderAsync(orderId, actorId, body?.Notes),
						OrderStatus.Refunded => await _orderServices.RefundOrderAsync(orderId, actorId, body?.Notes),
						OrderStatus.Returned => await _orderServices.ReturnOrderAsync(orderId, actorId, body?.Notes),
						OrderStatus.PaymentExpired => await _orderServices.ExpirePaymentAsync(orderId, actorId,false,true, body?.Notes),
						OrderStatus.CancelledByAdmin => await _orderServices.CancelOrderByAdminAsync(orderId, actorId),
						_ => Result<bool>.Fail("Unsupported status for admin endpoint", 400)
					};

					return HandleResult(result);
				}
				else
				{
					if (status != OrderStatus.CancelledByUser)
						return BadRequest(ApiResponse<bool>.CreateErrorResponse("Invalid Data", new ErrorResponse("Invalid Data", "Customers can only cancel their orders"), 403));

					var result = await _orderServices.CancelOrderByCustomerAsync(orderId, actorId);
					return HandleResult(result);
				}
			}
			catch (Exception ex)
			{
				_logger.LogError($"Error in UpdateOrderStatus: {ex.Message}");
				return StatusCode(500, ApiResponse<bool>.CreateErrorResponse("Server Error", new ErrorResponse("Server Error", "An error occurred while updating order status"), 500));
			}
		}

		#endregion

		#region RESTful Sub-resources

		/// <summary>
		/// Get order by order number (RESTful sub-resource)
		/// GET /api/order/number/{orderNumber}
		/// </summary>
		[HttpGet("number/{orderNumber}")]
		[Authorize(Roles = "User,Admin,SuperAdmin,DeliveryCompany")]
		public async Task<ActionResult<ApiResponse<OrderDto>>> GetOrderByNumber(string orderNumber)
		{
			try
			{
				_logger.LogInformation($"Executing GetOrderByNumber for number: {orderNumber}");
				var userId = GetUserId();
                var isAdmin = HasManagementRole();
                var result = await _orderServices.GetOrderByNumberAsync(orderNumber, userId,isAdmin);
				return HandleResult(result);
			}
			catch (Exception ex)
			{
				_logger.LogError($"Error in GetOrderByNumber: {ex.Message}");
				return StatusCode(500, ApiResponse<OrderDto>.CreateErrorResponse("Server Error", new ErrorResponse("Server Error", "An error occurred while retrieving the order"), 500));
			}
		}

		/// <summary>
		/// Get order count (RESTful)
		/// GET /api/order/count?userId={userId}
		/// - Admins can get count for any user
		/// - Customers get their own count
		/// </summary>
		//[HttpGet("count")]
		//[Authorize(Roles = "User,Admin")]
		//public async Task<ActionResult<ApiResponse<int?>>> GetOrderCount([FromQuery] string? userId = null)
		//{
		//	try
		//	{
		//		_logger.LogInformation("Executing GetOrderCount");
		//		var role = GetUserRole();
		//		var effectiveUserId = role == "Admin" ? userId : GetUserId();


		//		var result = await _orderServices.GetOrderCountByCustomerAsync(effectiveUserId ?? GetUserId());
		//		return HandleResult(result);
		//	}
		//	catch (Exception ex)
		//	{
		//		_logger.LogError($"Error in GetOrderCount: {ex.Message}");
		//		return StatusCode(500, ApiResponse<int?>.CreateErrorResponse("Server Error", new ErrorResponse("Server Error", "An error occurred while getting order count"), 500));
		//	}
		//}

		/// <summary>
		/// Get revenue statistics (RESTful)
		/// GET /api/order/revenue?userId={userId}
		/// - Admins can get revenue for any user
		/// - Customers get their own revenue
		/// </summary>
		[HttpGet("revenue/{userid}")]
		[Authorize(Roles = "User,Admin")]
		public async Task<ActionResult<ApiResponse<decimal>>> GetRevenueOfCustomer([FromRoute] string userId)
		{
			try
			{
				_logger.LogInformation("Executing GetRevenue");
				
				var effectiveUserId = HasManagementRole() ? userId : GetUserId();

				var result = await _orderServices.GetTotalRevenueByCustomerAsync(effectiveUserId);
				return HandleResult(result);
			}
			catch (Exception ex)
			{
				_logger.LogError($"Error in GetRevenue: {ex.Message}");
				return StatusCode(500, ApiResponse<decimal>.CreateErrorResponse("Server Error", new ErrorResponse("Server Error", "An error occurred while getting revenue"), 500));
			}
		}
		[HttpGet("revenue")]
		[Authorize(Roles = "Admin,SuperAdmin")]
		public async Task<ActionResult<ApiResponse<decimal>>> GetRevenue(DateTime start, DateTime end)
		{
			try
			{
				_logger.LogInformation("Executing GetRevenue");
			

				var result = await _orderServices.GetTotalRevenueByDateRangeAsync(start,end);
				return HandleResult(result);
			}
			catch (Exception ex)
			{
				_logger.LogError($"Error in GetRevenue: {ex.Message}");
				return StatusCode(500, ApiResponse<decimal>.CreateErrorResponse("Server Error", new ErrorResponse("Server Error", "An error occurred while getting revenue"), 500));
			}
		}

		#endregion

	


		
		[HttpGet("Count")]
		[Authorize(Roles = "Admin,SuperAdmin,DeliveryCompany")]
		public async Task<ActionResult<ApiResponse<int>>> CountOrdersAsync([FromQuery] OrderStatus? status = null,
		  [FromQuery] bool? IsDeleted = null)
		{
            _logger.LogInformation("Executing CountOrdersAsync");
			return HandleResult( await _orderServices.CountOrdersAsync(status, IsDeleted, true));


        }


	}
}