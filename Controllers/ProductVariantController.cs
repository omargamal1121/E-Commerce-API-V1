using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using E_Commerce.DtoModels.ProductDtos;
using E_Commerce.Enums;
using Microsoft.AspNetCore.Authorization;
using E_Commerce.DtoModels.Responses;
using E_Commerce.ErrorHnadling;
using E_Commerce.Services.ProductServices;

namespace E_Commerce.Controllers
{
    [ApiController]
    [Route("api/Products/{productId}/Variants")]
    public class ProductVariantController : ControllerBase
    {
        private readonly IProductVariantService _variantService;
        private readonly ILogger<ProductVariantController> _logger;

        public ProductVariantController(IProductVariantService variantService, ILogger<ProductVariantController> logger)
        {
            _variantService = variantService;
            _logger = logger;
        }

        private ActionResult<ApiResponse<T>> HandleResult<T>(E_Commerce.Services.Result<T> result, string actionName = null, int? id = null, int? productId = null) 
        {
            var apiResponse = result.Success
                ? ApiResponse<T>.CreateSuccessResponse(result.Message, result.Data, result.StatusCode, warnings: result.Warnings)
                : ApiResponse<T>.CreateErrorResponse(result.Message, new ErrorResponse("Error", result.Message), result.StatusCode, warnings: result.Warnings);

            switch (result.StatusCode)
            {
                case 200:
                    return Ok(apiResponse);
                case 201:
                    return actionName != null && id.HasValue ? CreatedAtAction(actionName, new { id, productId }, apiResponse) : StatusCode(201, apiResponse);
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

        // GET api/products/{productId}/variants
        [HttpGet]
        [AllowAnonymous]
        public async Task<ActionResult<ApiResponse<List<ProductVariantDto>>>> GetProductVariants(int productId, [FromQuery] bool? isActive = null, [FromQuery] bool? deletedOnly = null)
        {
            bool isAdmin = User?.IsInRole("Admin") == true;
            
            // For non-admin users, restrict to active and non-deleted variants
            if (!isAdmin)
            {
                isActive = true;
                deletedOnly = false;
            }
            
            var result = await _variantService.GetProductVariantsAsync(productId, isActive, deletedOnly);
            return HandleResult<List<ProductVariantDto>>(result);
        }

        // GET api/products/{productId}/variants/{id}
        [HttpGet("{id}")]
        [AllowAnonymous]
        public async Task<ActionResult<ApiResponse<ProductVariantDto>>> GetVariantById(int productId, int id)
        {
            var result = await _variantService.GetVariantByIdAsync(id);
            return HandleResult<ProductVariantDto>(result);
        }

        // POST api/products/{productId}/variants
        [HttpPost]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult<ApiResponse<ProductVariantDto>>> CreateVariant(int productId, [FromBody] CreateProductVariantDto dto)
        {
            if (!ModelState.IsValid)
            {
                var errors = string.Join(", ", ModelState.Values
                    .SelectMany(v => v.Errors)
                    .Select(e => e.ErrorMessage)
                    .ToList());
                _logger.LogError($"Validation Errors: {errors}");
                return BadRequest(ApiResponse<ProductVariantDto>.CreateErrorResponse("Invalid variant data", new ErrorResponse("Invalid data", errors)));
            }
            
            var userId = HttpContext.Items["UserId"]?.ToString();
            var result = await _variantService.AddVariantAsync(productId, dto, userId);
            return HandleResult<ProductVariantDto>(result, nameof(GetVariantById), result.Data?.Id, productId);
        }

        // PUT api/products/{productId}/variants/{id}
        [HttpPut("{id}")]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult<ApiResponse<ProductVariantDto>>> UpdateVariant(int productId, int id, [FromBody] UpdateProductVariantDto dto)
        {
            if (!ModelState.IsValid)
            {
                var errors = string.Join(", ", ModelState.Values
                    .SelectMany(v => v.Errors)
                    .Select(e => e.ErrorMessage)
                    .ToList());
                _logger.LogError($"Validation Errors: {errors}");
                return BadRequest(ApiResponse<ProductVariantDto>.CreateErrorResponse("Invalid variant data", new ErrorResponse("Invalid data", errors)));
            }
            
            var userId = HttpContext.Items["UserId"]?.ToString();
            var result = await _variantService.UpdateVariantAsync(id, dto, userId);
            return HandleResult<ProductVariantDto>(result);
        }

        // DELETE api/products/{productId}/variants/{id}
        [HttpDelete("{id}")]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult<ApiResponse<bool>>> DeleteVariant(int productId, int id)
        {
            var userId = HttpContext.Items["UserId"]?.ToString();
            var result = await _variantService.DeleteVariantAsync(id, userId);
            return HandleResult<bool>(result);
        }

        // PATCH api/products/{productId}/variants/{id}/quantity/add
        [HttpPatch("{id}/quantity/add")]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult<ApiResponse<bool>>> AddVariantQuantity(int productId, int id, [FromQuery] int quantity)
        {
            if (!ModelState.IsValid)
            {
                var errors = string.Join(", ", ModelState.Values
                    .SelectMany(v => v.Errors)
                    .Select(e => e.ErrorMessage)
                    .ToList());
                _logger.LogError($"Validation Errors: {errors}");
                return BadRequest(ApiResponse<bool>.CreateErrorResponse("Invalid quantity data", new ErrorResponse("Invalid data", errors)));
            }
            
            if (quantity <= 0)
            {
                return BadRequest(ApiResponse<bool>.CreateErrorResponse("Invalid quantity", new ErrorResponse("Invalid data", "Quantity must be greater than 0")));
            }
            
            var userId = HttpContext.Items["UserId"]?.ToString();
            var result = await _variantService.AddVariantQuantityAsync(id, quantity, userId);
            return HandleResult<bool>(result);
        }

        // PATCH api/products/{productId}/variants/{id}/quantity/remove
        [HttpPatch("{id}/quantity/remove")]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult<ApiResponse<bool>>> RemoveVariantQuantity(int productId, int id, [FromQuery] int quantity)
        {
            if (!ModelState.IsValid)
            {
                var errors = string.Join(", ", ModelState.Values
                    .SelectMany(v => v.Errors)
                    .Select(e => e.ErrorMessage)
                    .ToList());
                _logger.LogError($"Validation Errors: {errors}");
                return BadRequest(ApiResponse<bool>.CreateErrorResponse("Invalid quantity data", new ErrorResponse("Invalid data", errors)));
            }
            
            if (quantity <= 0)
            {
                return BadRequest(ApiResponse<bool>.CreateErrorResponse("Invalid quantity", new ErrorResponse("Invalid data", "Quantity must be greater than 0")));
            }
            
            var userId = HttpContext.Items["UserId"]?.ToString();
            var result = await _variantService.RemoveVariantQuantityAsync(id, quantity, userId);
            return HandleResult<bool>(result);
        }

        // PATCH api/products/{productId}/variants/{id}/activate
        [HttpPatch("{id}/activate")]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult<ApiResponse<bool>>> ActivateVariant(int productId, int id)
        {
            var userId = HttpContext.Items["UserId"]?.ToString();
            var result = await _variantService.ActivateVariantAsync(id, userId);
            return HandleResult<bool>(result);
        }

        // PATCH api/products/{productId}/variants/{id}/deactivate
        [HttpPatch("{id}/deactivate")]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult<ApiResponse<bool>>> DeactivateVariant(int productId, int id)
        {
            var userId = HttpContext.Items["UserId"]?.ToString();
            var result = await _variantService.DeactivateVariantAsync(id, userId);
            return HandleResult<bool>(result);
        }

        // PATCH api/products/{productId}/variants/{id}/restore
        [HttpPatch("{id}/restore")]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult<ApiResponse<bool>>> RestoreVariant(int productId, int id)
        {
            var userId = HttpContext.Items["UserId"]?.ToString();
            var result = await _variantService.RestoreVariantAsync(id, userId);
            return HandleResult<bool>(result);
        }

        // GET api/products/{productId}/variants/search
        [HttpGet("search")]
        [AllowAnonymous]
        public async Task<ActionResult<ApiResponse<List<ProductVariantDto>>>> SearchVariants(int productId, [FromQuery] string? color = null, [FromQuery] int? length = null, [FromQuery] int? waist = null, [FromQuery] VariantSize? size = null, [FromQuery] bool? isActive = null, [FromQuery] bool? deletedOnly = null)
        {
            bool isAdmin = User?.IsInRole("Admin") == true;
            
            // For non-admin users, restrict to active and non-deleted variants
            if (!isAdmin)
            {
                isActive = true;
                deletedOnly = false;
            }
            
            var result = await _variantService.GetVariantsBySearchAsync(productId, color, length, waist, size, isActive, deletedOnly);
            return HandleResult<List<ProductVariantDto>>(result);
        }
    }
}