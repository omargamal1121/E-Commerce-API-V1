using E_Commerce.DtoModels.Responses;
using E_Commerce.ErrorHnadling;
using E_Commerce.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System.Security.Claims;
using System.Linq;
using E_Commerce.Services;

namespace E_Commerce.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class WishlistController : ControllerBase
    {
        private readonly IWishlistService _wishlistService;
        private readonly ILogger<WishlistController> _logger;

        public WishlistController(IWishlistService wishlistService, ILogger<WishlistController> logger)
        {
            _wishlistService = wishlistService;
            _logger = logger;
        }

        private string GetUserId()
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

            return result.StatusCode switch
            {
                200 => Ok(apiResponse),
                201 => actionName != null && id.HasValue ? CreatedAtAction(actionName, new { id }, apiResponse) : StatusCode(201, apiResponse),
                400 => BadRequest(apiResponse),
                401 => Unauthorized(apiResponse),
                404 => NotFound(apiResponse),
                409 => Conflict(apiResponse),
                _ => StatusCode(result.StatusCode, apiResponse)
            };
        }

        // GET: api/wishlist
        [HttpGet]
        [Authorize(Roles = "Customer,Admin")]
        public async Task<ActionResult<ApiResponse<List<E_Commerce.DtoModels.ProductDtos.WishlistItemDto>>>> GetWishlist([FromQuery] int page = 1, [FromQuery] int pageSize = 20)
        {
            try
            {
                if (page <= 0 || pageSize <= 0)
                {
                    return BadRequest(ApiResponse<string>.CreateErrorResponse(
                        "Validation error",
                        new ErrorResponse("ValidationError", "page and pageSize must be greater than 0"),
                        400));
                }

                var userId = GetUserId();
                if (string.IsNullOrWhiteSpace(userId))
                {
                    return Unauthorized(ApiResponse<string>.CreateErrorResponse(
                        "Authentication required",
                        new ErrorResponse("Unauthorized", "User is not authenticated"),
                        401));
                }

                var result = await _wishlistService.GetWishlistAsync(userId, page, pageSize);
                return HandleResult(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in GetWishlist");
                return StatusCode(500, ApiResponse<string>.CreateErrorResponse("Internal server error", new ErrorResponse("Exception", ex.Message), 500));
            }
        }

        // POST: api/wishlist/{productId}
        [HttpPost("{productId:int}")]
        [Authorize(Roles = "Customer,Admin")]
        public async Task<ActionResult<ApiResponse<bool>>> AddToWishlist([FromRoute] int productId)
        {
            try
            {
                if (productId <= 0)
                {
                    return BadRequest(ApiResponse<string>.CreateErrorResponse(
                        "Validation error",
                        new ErrorResponse("ValidationError", "productId must be greater than 0"),
                        400));
                }

                var userId = GetUserId();
                if (string.IsNullOrWhiteSpace(userId))
                {
                    return Unauthorized(ApiResponse<string>.CreateErrorResponse(
                        "Authentication required",
                        new ErrorResponse("Unauthorized", "User is not authenticated"),
                        401));
                }

                var result = await _wishlistService.AddAsync(userId, productId);
                return HandleResult(result, nameof(AddToWishlist));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in AddToWishlist");
                return StatusCode(500, ApiResponse<string>.CreateErrorResponse("Internal server error", new ErrorResponse("Exception", ex.Message), 500));
            }
        }

        // DELETE: api/wishlist/{productId}
        [HttpDelete("{productId:int}")]
        [Authorize(Roles = "Customer,Admin")]
        public async Task<ActionResult<ApiResponse<bool>>> RemoveFromWishlist([FromRoute] int productId)
        {
            try
            {
                if (productId <= 0)
                {
                    return BadRequest(ApiResponse<string>.CreateErrorResponse(
                        "Validation error",
                        new ErrorResponse("ValidationError", "productId must be greater than 0"),
                        400));
                }

                var userId = GetUserId();
                if (string.IsNullOrWhiteSpace(userId))
                {
                    return Unauthorized(ApiResponse<string>.CreateErrorResponse(
                        "Authentication required",
                        new ErrorResponse("Unauthorized", "User is not authenticated"),
                        401));
                }

                var result = await _wishlistService.RemoveAsync(userId, productId);
                return HandleResult(result, nameof(RemoveFromWishlist), productId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in RemoveFromWishlist");
                return StatusCode(500, ApiResponse<string>.CreateErrorResponse("Internal server error", new ErrorResponse("Exception", ex.Message), 500));
            }
        }

        // DELETE: api/wishlist
        [HttpDelete]
        [Authorize(Roles = "Customer,Admin")]
        public async Task<ActionResult<ApiResponse<bool>>> ClearWishlist()
        {
            try
            {
                var userId = GetUserId();
                if (string.IsNullOrWhiteSpace(userId))
                {
                    return Unauthorized(ApiResponse<string>.CreateErrorResponse(
                        "Authentication required",
                        new ErrorResponse("Unauthorized", "User is not authenticated"),
                        401));
                }

                var result = await _wishlistService.ClearAsync(userId);
                return HandleResult(result, nameof(ClearWishlist));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in ClearWishlist");
                return StatusCode(500, ApiResponse<string>.CreateErrorResponse("Internal server error", new ErrorResponse("Exception", ex.Message), 500));
            }
        }

        // GET: api/wishlist/contains/{productId}
        [HttpGet("contains/{productId:int}")]
        [Authorize(Roles = "Customer,Admin")]
        public async Task<ActionResult<ApiResponse<bool>>> IsInWishlist([FromRoute] int productId)
        {
            try
            {
                if (productId <= 0)
                {
                    return BadRequest(ApiResponse<string>.CreateErrorResponse(
                        "Validation error",
                        new ErrorResponse("ValidationError", "productId must be greater than 0"),
                        400));
                }

                var userId = GetUserId();
                if (string.IsNullOrWhiteSpace(userId))
                {
                    return Unauthorized(ApiResponse<string>.CreateErrorResponse(
                        "Authentication required",
                        new ErrorResponse("Unauthorized", "User is not authenticated"),
                        401));
                }

                var result = await _wishlistService.IsInWishlistAsync(userId, productId);
                return HandleResult(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in IsInWishlist");
                return StatusCode(500, ApiResponse<string>.CreateErrorResponse("Internal server error", new ErrorResponse("Exception", ex.Message), 500));
            }
        }
    }
}
