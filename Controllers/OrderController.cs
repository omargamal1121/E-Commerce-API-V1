using E_Commerce.DtoModels.OrderDtos;
using E_Commerce.DtoModels.Responses;
using E_Commerce.Enums;
using E_Commerce.ErrorHnadling;
using E_Commerce.Interfaces;
using E_Commerce.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace E_Commerce.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class OrderController : ControllerBase
    {
        private readonly IOrderServices _orderServices;
        private readonly ILogger<OrderController> _logger;

        public OrderController(IOrderServices orderServices, ILogger<OrderController> logger)
        {
            _orderServices = orderServices;
            _logger = logger;
        }

        private string GetUserId()
        {
            return User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? string.Empty;
        }

        private string GetUserRole()
        {
            return User.FindFirst(ClaimTypes.Role)?.Value ?? "Customer";
        }

        private List<string> GetModelErrors()
        {
            return ModelState.Values
                .SelectMany(v => v.Errors)
                .Select(e => e.ErrorMessage)
                .ToList();
        }

        private ActionResult<ApiResponse<T>> HandleResult<T>(Result<T> result, string? actionName = null, int? id = null)
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

        #region Customer/User Endpoints

        /// <summary>
        /// Get order by ID (Customer access - own orders only)
        /// </summary>
        [HttpGet("{orderId}")]
        [Authorize(Roles = "Customer,Admin")]
        public async Task<ActionResult<ApiResponse<OrderDto>>> GetOrderById(int orderId)
        {
            try
            {
                _logger.LogInformation($"Executing GetOrderById for ID: {orderId}");
                var userId = GetUserId();
                var result = await _orderServices.GetOrderByIdAsync(orderId, userId);
                return HandleResult(result, nameof(GetOrderById), orderId);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error in GetOrderById: {ex.Message}");
                return StatusCode(500, ApiResponse<OrderDto>.CreateErrorResponse("Server Error", new ErrorResponse("Server Error", "An error occurred while retrieving the order"), 500));
            }
        }

        /// <summary>
        /// Get order by order number (Customer access - own orders only)
        /// </summary>
        [HttpGet("number/{orderNumber}")]
        [Authorize(Roles = "Customer,Admin")]
        public async Task<ActionResult<ApiResponse<OrderDto>>> GetOrderByNumber(string orderNumber)
        {
            try
            {
                _logger.LogInformation($"Executing GetOrderByNumber for number: {orderNumber}");
                var userId = GetUserId();
                var result = await _orderServices.GetOrderByNumberAsync(orderNumber, userId);
                return HandleResult(result, nameof(GetOrderByNumber));
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error in GetOrderByNumber: {ex.Message}");
                return StatusCode(500, ApiResponse<OrderDto>.CreateErrorResponse("Server Error", new ErrorResponse("Server Error", "An error occurred while retrieving the order"), 500));
            }
        }

        /// <summary>
        /// Get customer orders with pagination (Customer access - own orders only)
        /// </summary>
        [HttpGet("customer")]
        [Authorize(Roles = "Customer,Admin")]
        public async Task<ActionResult<ApiResponse<List<OrderListDto>>>> GetCustomerOrders(
            [FromQuery] int page = 1, 
            [FromQuery] int pageSize = 10)
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

                _logger.LogInformation($"Executing GetCustomerOrders with pagination: page {page}, size {pageSize}");
                var userId = GetUserId();
                var result = await _orderServices.GetCustomerOrdersAsync(userId, false, page, pageSize);
                return HandleResult(result, nameof(GetCustomerOrders));
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error in GetCustomerOrders: {ex.Message}");
                return StatusCode(500, ApiResponse<List<OrderListDto>>.CreateErrorResponse("Server Error", new ErrorResponse("Server Error", "An error occurred while retrieving customer orders"), 500));
            }
        }

        /// <summary>
        /// Create order from cart (Customer access)
        /// </summary>
        [HttpPost("create-from-cart")]
        [Authorize()]
        public async Task<ActionResult<ApiResponse<OrderWithPaymentDto>>> CreateOrderFromCart([FromBody] CreateOrderDto orderDto)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    var errors = GetModelErrors();
                    _logger.LogWarning($"ModelState errors: {string.Join(", ", errors)}");
                    return BadRequest(ApiResponse<OrderDto>.CreateErrorResponse("Invalid Data", new ErrorResponse("Invalid Data", errors), 400));
                }

                _logger.LogInformation("Executing CreateOrderFromCart");
                var userId = GetUserId();
                var result = await _orderServices.CreateOrderFromCartAsync(userId, orderDto);
                return HandleResult(result, nameof(CreateOrderFromCart));
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error in CreateOrderFromCart: {ex.Message}");
                return StatusCode(500, ApiResponse<OrderDto>.CreateErrorResponse("Server Error", new ErrorResponse("Server Error", "An error occurred while creating the order"), 500));
            }
        }

        /// <summary>
        /// Cancel an order (Customer access - own orders only)
        /// </summary>
        [HttpPost("{orderId}/cancel")]
        [Authorize(Roles = "Customer,Admin")]
        public async Task<ActionResult<ApiResponse<string>>> CancelOrder(int orderId, [FromBody] CancelOrderDto cancelDto)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    var errors = GetModelErrors();
                    _logger.LogWarning($"ModelState errors: {string.Join(", ", errors)}");
                    return BadRequest(ApiResponse<string>.CreateErrorResponse("Invalid Data", new ErrorResponse("Invalid Data", errors), 400));
                }

                _logger.LogInformation($"Executing CancelOrder for ID: {orderId}");
                var userId = GetUserId();
                var result = await _orderServices.CancelOrderAsync(orderId, cancelDto, userId);
                return HandleResult(result, nameof(CancelOrder), orderId);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error in CancelOrder: {ex.Message}");
                return StatusCode(500, ApiResponse<string>.CreateErrorResponse("Server Error", new ErrorResponse("Server Error", "An error occurred while canceling the order"), 500));
            }
        }

        /// <summary>
        /// Get customer order count (Customer access - own count only)
        /// </summary>
        [HttpGet("customer/count")]
        [Authorize(Roles = "Customer,Admin")]
        public async Task<ActionResult<ApiResponse<int?>>> GetOrderCount()
        {
            try
            {
                _logger.LogInformation("Executing GetOrderCount");
                var userId = GetUserId();
                var result = await _orderServices.GetOrderCountByCustomerAsync(userId);
                return HandleResult(result, nameof(GetOrderCount));
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error in GetOrderCount: {ex.Message}");
                return StatusCode(500, ApiResponse<int?>.CreateErrorResponse("Server Error", new ErrorResponse("Server Error", "An error occurred while getting order count"), 500));
            }
        }

        /// <summary>
        /// Get customer total revenue (Customer access - own revenue only)
        /// </summary>
        [HttpGet("customer/revenue")]
        [Authorize(Roles = "Customer,Admin")]
        public async Task<ActionResult<ApiResponse<decimal>>> GetCustomerRevenue()
        {
            try
            {
                _logger.LogInformation("Executing GetCustomerRevenue");
                var userId = GetUserId();
                var result = await _orderServices.GetTotalRevenueByCustomerAsync(userId);
                return HandleResult(result, nameof(GetCustomerRevenue));
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error in GetCustomerRevenue: {ex.Message}");
                return StatusCode(500, ApiResponse<decimal>.CreateErrorResponse("Server Error", new ErrorResponse("Server Error", "An error occurred while getting customer revenue"), 500));
            }
        }

        #endregion

        #region Admin-Only Endpoints

        /// <summary>
        /// Get orders by status (Admin only)
        /// </summary>
        [HttpGet("status/{status}")]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult<ApiResponse<List<OrderListDto>>>> GetOrdersByStatus(
            OrderStatus status,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 10)
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

                _logger.LogInformation($"Executing GetOrdersByStatus for status: {status}, page: {page}, size: {pageSize}");
                var result = await _orderServices.FilterOrdersAsync(status: status, page: page, pageSize: pageSize);
                return HandleResult(result, nameof(GetOrdersByStatus));
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error in GetOrdersByStatus: {ex.Message}");
                return StatusCode(500, ApiResponse<List<OrderListDto>>.CreateErrorResponse("Server Error", new ErrorResponse("Server Error", "An error occurred while retrieving orders by status"), 500));
            }
        }

        /// <summary>
        /// Update order status (Admin only)
        /// </summary>
        [HttpPut("{orderId}/status")]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult<ApiResponse<bool>>> UpdateOrderStatus(
            int orderId, 
            [FromBody] UpdateOrderStatusDto statusDto)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    var errors = GetModelErrors();
                    _logger.LogWarning($"ModelState errors: {string.Join(", ", errors)}");
                    return BadRequest(ApiResponse<bool>.CreateErrorResponse("Invalid Data", new ErrorResponse("Invalid Data", errors), 400));
                }

                _logger.LogInformation($"Executing UpdateOrderStatus for ID: {orderId}, status: {statusDto.Status}");
                var result = await _orderServices.UpdateOrderStatusAsync(orderId, statusDto);
                return HandleResult(result, nameof(UpdateOrderStatus), orderId);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error in UpdateOrderStatus: {ex.Message}");
                return StatusCode(500, ApiResponse<OrderDto>.CreateErrorResponse("Server Error", new ErrorResponse("Server Error", "An error occurred while updating order status"), 500));
            }
        }

        /// <summary>
        /// Ship an order (Admin only)
        /// </summary>
        [HttpPost("{orderId}/ship")]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult<ApiResponse<string>>> ShipOrder(int orderId)
        {
            try
            {
                _logger.LogInformation($"Executing ShipOrder for ID: {orderId}");
                var result = await _orderServices.ShipOrderAsync(orderId);
                return HandleResult(result, nameof(ShipOrder), orderId);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error in ShipOrder: {ex.Message}");
                return StatusCode(500, ApiResponse<string>.CreateErrorResponse("Server Error", new ErrorResponse("Server Error", "An error occurred while shipping the order"), 500));
            }
        }

        /// <summary>
        /// Deliver an order (Admin only)
        /// </summary>
        [HttpPost("{orderId}/deliver")]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult<ApiResponse<string>>> DeliverOrder(int orderId)
        {
            try
            {
                _logger.LogInformation($"Executing DeliverOrder for ID: {orderId}");
                var result = await _orderServices.DeliverOrderAsync(orderId);
                return HandleResult(result, nameof(DeliverOrder), orderId);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error in DeliverOrder: {ex.Message}");
                return StatusCode(500, ApiResponse<string>.CreateErrorResponse("Server Error", new ErrorResponse("Server Error", "An error occurred while delivering the order"), 500));
            }
        }

        /// <summary>
        /// Get revenue by date range (Admin only)
        /// </summary>
        //[HttpGet("revenue")]
        //[Authorize(Roles = "Admin")]
        //public async Task<ActionResult<ApiResponse<decimal>>> GetRevenueByDateRange(
        //    [FromQuery] DateTime startDate,
        //    [FromQuery] DateTime endDate)
        //{
        //    try
        //    {
        //        _logger.LogInformation($"Executing GetRevenueByDateRange from {startDate} to {endDate}");
        //        var userRole = GetUserRole();
        //        var result = await _orderServices.GetTotalRevenueByDateRangeAsync(startDate, endDate, userRole);
        //        return HandleResult(result, nameof(GetRevenueByDateRange));
        //    }
        //    catch (Exception ex)
        //    {
        //        _logger.LogError($"Error in GetRevenueByDateRange: {ex.Message}");
        //        return StatusCode(500, ApiResponse<decimal>.CreateErrorResponse("Server Error", new ErrorResponse("Server Error", "An error occurred while getting revenue by date range"), 500));
        //    }
        //}

        /// <summary>
        /// Filter orders with comprehensive filtering options (Admin only)
        /// </summary>
        [HttpGet("filter")]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult<ApiResponse<List<OrderListDto>>>> FilterOrders(
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

                _logger.LogInformation($"Executing FilterOrders: userId: {userId}, deleted: {deleted}, page: {page}, size: {pageSize}, status: {status}");
                var result = await _orderServices.FilterOrdersAsync(userId, deleted, page, pageSize, status);
                return HandleResult(result, nameof(FilterOrders));
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error in FilterOrders: {ex.Message}");
                return StatusCode(500, ApiResponse<List<OrderListDto>>.CreateErrorResponse("Server Error", new ErrorResponse("Server Error", "An error occurred while filtering orders"), 500));
            }
        }

        /// <summary>
        /// Get orders with pagination (Admin only) - Legacy endpoint, use /filter instead
        /// </summary>
        [HttpGet("admin")]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult<ApiResponse<List<OrderListDto>>>> GetOrdersWithPagination(
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

                _logger.LogInformation($"Executing GetOrdersWithPagination (legacy): page {page}, size {pageSize}, status: {status}");
                // Use the new FilterOrdersAsync method internally
                var result = await _orderServices.FilterOrdersAsync(null, false, page, pageSize, status);
                return HandleResult(result, nameof(GetOrdersWithPagination));
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error in GetOrdersWithPagination: {ex.Message}");
                return StatusCode(500, ApiResponse<List<OrderListDto>>.CreateErrorResponse("Server Error", new ErrorResponse("Server Error", "An error occurred while retrieving orders with pagination"), 500));
            }
        }

        /// <summary>
        /// Get total order count (Admin only)
        /// </summary>
        [HttpGet("admin/count")]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult<ApiResponse<int?>>> GetTotalOrderCount(
            [FromQuery] OrderStatus? status = null)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    var errors = GetModelErrors();
                    _logger.LogWarning($"ModelState errors: {string.Join(", ", errors)}");
                    return BadRequest(ApiResponse<int?>.CreateErrorResponse("Invalid Data", new ErrorResponse("Invalid Data", errors), 400));
                }

                _logger.LogInformation($"Executing GetTotalOrderCount, status: {status}");
                var userRole = GetUserRole();
                var result = await _orderServices.GetTotalOrderCountAsync(status, userRole);
                return HandleResult(result, nameof(GetTotalOrderCount));
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error in GetTotalOrderCount: {ex.Message}");
                return StatusCode(500, ApiResponse<int?>.CreateErrorResponse("Server Error", new ErrorResponse("Server Error", "An error occurred while getting total order count"), 500));
            }
        }

        #endregion
    }
}