
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

using ApplicationLayer.DtoModels.Responses;
using ApplicationLayer.Interfaces;
using ApplicationLayer.DtoModels.ProductDtos;
using ApplicationLayer.ErrorHnadling;

namespace DomainLayer.Controllers
{
    [ApiController]
   
    [Route("api/[controller]")]
    public class WishlistController : BaseController
    {
        private readonly IWishlistService _wishlistService;
        private readonly ILogger<WishlistController> _logger;

        public WishlistController(IWishlistService wishlistService, ILogger<WishlistController> logger)
        {
            _wishlistService = wishlistService;
            _logger = logger;
        }

        [HttpGet]

        public async Task<ActionResult<ApiResponse<List<WishlistItemDto>>>> GetWishlist([FromQuery] int page = 1, [FromQuery] int pageSize = 20)
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
        [Authorize]

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
        [Authorize]

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
        [Authorize]
        public async Task<ActionResult<ApiResponse<bool>>> ClearWishlist()
        {
            try
            {
                var userId = GetUserId();
              

                var result = await _wishlistService.ClearAsync(userId);
                return HandleResult(result, nameof(ClearWishlist));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in ClearWishlist");
                return StatusCode(500, ApiResponse<string>.CreateErrorResponse("Internal server error", new ErrorResponse("Exception", ex.Message), 500));
            }
        }

        [HttpGet("contains/{productId:int}")]
        [Authorize]
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
