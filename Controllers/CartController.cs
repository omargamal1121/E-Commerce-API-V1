using E_Commerce.DtoModels.CartDtos;
using E_Commerce.DtoModels.Responses;
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
    [Authorize]
    public class CartController : ControllerBase
    {
        private readonly ICartServices _cartServices;
        private readonly ILogger<CartController> _logger;

        public CartController(ICartServices cartServices, ILogger<CartController> logger)
        {
            _cartServices = cartServices;
            _logger = logger;
        }

        private string GetCurrentUserId()
        {
            return User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? string.Empty;
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

        /// <summary>
        /// Get the current user's cart
        /// </summary>
        /// <returns>Cart details with items</returns>
        [HttpGet]
        public async Task<ActionResult<ApiResponse<CartDto>>> GetCart()
        {
            try
            {
                var userId = HttpContext.Items["UserId"].ToString();

                _logger.LogInformation("Executing GetCart");
                var result = await _cartServices.GetCartAsync(userId);
                return HandleResult(result, nameof(GetCart));
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error in GetCart: {ex.Message}");
                return StatusCode(500, ApiResponse<CartDto>.CreateErrorResponse("Server Error", new ErrorResponse("Server Error", "An error occurred while retrieving cart"), 500));
            }
        }

        /// <summary>
        /// Add an item to the cart
        /// </summary>
        /// <param name="itemDto">Item details to add</param>
        /// <returns>Success/failure</returns>
        [HttpPost("items")]
        public async Task<ActionResult<ApiResponse<bool>>> AddItemToCart([FromBody] CreateCartItemDto itemDto)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    var errors = GetModelErrors();
                    _logger.LogWarning($"ModelState errors: {string.Join(", ", errors)}");
                    return BadRequest(ApiResponse<bool>.CreateErrorResponse("Invalid Data", new ErrorResponse("Invalid Data", errors), 400));
                }

                var userId = GetCurrentUserId();
                if (string.IsNullOrEmpty(userId))
                {
                    return Unauthorized(ApiResponse<bool>.CreateErrorResponse("Unauthorized", new ErrorResponse("Unauthorized", "User not authenticated"), 401));
                }

                _logger.LogInformation($"Executing AddItemToCart for product ID: {itemDto.ProductId}");
                var result = await _cartServices.AddItemToCartAsync(userId, itemDto);
                return HandleResult(result, nameof(AddItemToCart));
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error in AddItemToCart: {ex.Message}");
                return StatusCode(500, ApiResponse<bool>.CreateErrorResponse("Server Error", new ErrorResponse("Server Error", "An error occurred while adding item to cart"), 500));
            }
        }

        /// <summary>
        /// Update the quantity of an item in the cart
        /// </summary>
        /// <param name="productId">Product ID</param>
        /// <param name="itemDto">Updated item details</param>
        /// <param name="productVariantId">Product variant ID (optional)</param>
        /// <returns>Success/failure</returns>
        [HttpPut("items/{productId}/{variantId}")]
        public async Task<ActionResult<ApiResponse<bool>>> UpdateCartItem(int productId, [FromBody] UpdateCartItemDto itemDto, [FromRoute] int variantId)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    var errors = GetModelErrors();
                    _logger.LogWarning($"ModelState errors: {string.Join(", ", errors)}");
                    return BadRequest(ApiResponse<bool>.CreateErrorResponse("Invalid Data", new ErrorResponse("Invalid Data", errors), 400));
                }

                var userId = GetCurrentUserId();
                if (string.IsNullOrEmpty(userId))
                {
                    return Unauthorized(ApiResponse<bool>.CreateErrorResponse("Unauthorized", new ErrorResponse("Unauthorized", "User not authenticated"), 401));
                }

                _logger.LogInformation($"Executing UpdateCartItem for product ID: {productId}, variant ID: {variantId}");
                var result = await _cartServices.UpdateCartItemAsync(userId, productId, itemDto, variantId);
                return HandleResult(result, nameof(UpdateCartItem), productId);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error in UpdateCartItem: {ex.Message}");
                return StatusCode(500, ApiResponse<bool>.CreateErrorResponse("Server Error", new ErrorResponse("Server Error", "An error occurred while updating cart item"), 500));
            }
        }

        /// <summary>
        /// Remove an item from the cart
        /// </summary>
        /// <param name="itemDto">Item details to remove</param>
        /// <returns>Success/failure</returns>
        [HttpDelete("items")]
        public async Task<ActionResult<ApiResponse<bool>>> RemoveItemFromCart([FromBody] RemoveCartItemDto itemDto)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    var errors = GetModelErrors();
                    _logger.LogWarning($"ModelState errors: {string.Join(", ", errors)}");
                    return BadRequest(ApiResponse<bool>.CreateErrorResponse("Invalid Data", new ErrorResponse("Invalid Data", errors), 400));
                }

                var userId = GetCurrentUserId();
                if (string.IsNullOrEmpty(userId))
                {
                    return Unauthorized(ApiResponse<bool>.CreateErrorResponse("Unauthorized", new ErrorResponse("Unauthorized", "User not authenticated"), 401));
                }

                _logger.LogInformation($"Executing RemoveItemFromCart for product ID: {itemDto.ProductId}");
                var result = await _cartServices.RemoveItemFromCartAsync(userId, itemDto);
                return HandleResult(result, nameof(RemoveItemFromCart));
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error in RemoveItemFromCart: {ex.Message}");
                return StatusCode(500, ApiResponse<bool>.CreateErrorResponse("Server Error", new ErrorResponse("Server Error", "An error occurred while removing item from cart"), 500));
            }
        }

        /// <summary>
        /// Clear all items from the cart
        /// </summary>
        /// <returns>Success/failure</returns>
        [HttpDelete("clear")]
        public async Task<ActionResult<ApiResponse<bool>>> ClearCart()
        {
            try
            {
                var userId = GetCurrentUserId();
                if (string.IsNullOrEmpty(userId))
                {
                    return Unauthorized(ApiResponse<bool>.CreateErrorResponse("Unauthorized", new ErrorResponse("Unauthorized", "User not authenticated"), 401));
                }

                _logger.LogInformation("Executing ClearCart");
                var result = await _cartServices.ClearCartAsync(userId);
                return HandleResult(result, nameof(ClearCart));
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error in ClearCart: {ex.Message}");
                return StatusCode(500, ApiResponse<bool>.CreateErrorResponse("Server Error", new ErrorResponse("Server Error", "An error occurred while clearing cart"), 500));
            }
        }

        /// <summary>
        /// Get the total number of items in the cart
        /// </summary>
        /// <returns>Item count</returns>
        [HttpGet("items/count")]
        public async Task<ActionResult<ApiResponse<int?>>> GetCartItemCount()
        {
            try
            {
                var userId = GetCurrentUserId();
                if (string.IsNullOrEmpty(userId))
                {
                    return Unauthorized(ApiResponse<int>.CreateErrorResponse("Unauthorized", new ErrorResponse("Unauthorized", "User not authenticated"), 401));
                }

                _logger.LogInformation("Executing GetCartItemCount");
                var result = await _cartServices.GetCartItemCountAsync(userId);
                return HandleResult(result, nameof(GetCartItemCount));
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error in GetCartItemCount: {ex.Message}");
                return StatusCode(500, ApiResponse<int>.CreateErrorResponse("Server Error", new ErrorResponse("Server Error", "An error occurred while getting cart item count"), 500));
            }
        }

       
      
        /// <summary>
        /// Check if the cart is empty
        /// </summary>
        /// <returns>True if cart is empty, false otherwise</returns>
        [HttpGet("empty")]
        public async Task<ActionResult<ApiResponse<bool>>> IsCartEmpty()
        {
            try
            {
                var userId = GetCurrentUserId();
                if (string.IsNullOrEmpty(userId))
                {
                    return Unauthorized(ApiResponse<bool>.CreateErrorResponse("Unauthorized", new ErrorResponse("Unauthorized", "User not authenticated"), 401));
                }

                _logger.LogInformation("Executing IsCartEmpty");
                var result = await _cartServices.IsCartEmptyAsync(userId);
                return HandleResult(result, nameof(IsCartEmpty));
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error in IsCartEmpty: {ex.Message}");
                return StatusCode(500, ApiResponse<bool>.CreateErrorResponse("Server Error", new ErrorResponse("Server Error", "An error occurred while checking if cart is empty"), 500));
            }
        }
        [HttpPost("checkout")]
       
        public async Task<ActionResult<ApiResponse<bool>>> CheckOut()
        {
            try
            {
                var userId = GetCurrentUserId();
                if (string.IsNullOrEmpty(userId))
                {
                    return Unauthorized(ApiResponse<bool>.CreateErrorResponse("Unauthorized", new ErrorResponse("Unauthorized", "User not authenticated"), 401));
                }

                _logger.LogInformation("Executing CheckOut");
                var result = await _cartServices.UpdateCheckoutData(userId);
                return HandleResult(result, nameof(IsCartEmpty));
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error in CheckOut: {ex.Message}");
                return StatusCode(500, ApiResponse<bool>.CreateErrorResponse("Server Error", new ErrorResponse("Server Error", "An error occurred while checking if cart is CheckOut"), 500));
            }
        }
    }
}