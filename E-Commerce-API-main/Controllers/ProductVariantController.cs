using Microsoft.AspNetCore.Mvc;

using DomainLayer.Enums;
using Microsoft.AspNetCore.Authorization;

using ApplicationLayer.DtoModels.Responses;
using ApplicationLayer.DtoModels.ProductDtos;
using ApplicationLayer.ErrorHnadling;
using ApplicationLayer.Services.ProductVariantServices;
using ApplicationLayer.Interfaces;

namespace DomainLayer.Controllers
{
    [ApiController]
    [Route("api/Products/{productId}/Variants")]
    public class ProductVariantController : BaseController
    {
        private readonly IProductVariantService _variantService;
        private readonly ILogger<ProductVariantController> _logger;
        

        public ProductVariantController(IProductVariantService variantService, ILogger<ProductVariantController> logger,IProductLinkBuilder linkBuilder):base(linkBuilder)
        {
            _variantService = variantService;
            _logger = logger;
        }

       
        // GET api/products/{productId}/variants/{id}
        [HttpGet("{id}")]
        [AllowAnonymous]
        public async Task<ActionResult<ApiResponse<ProductVariantDto>>> GetVariantById(int productId, int id)
        {
            var result = await _variantService.GetVariantByIdAsync(id);
            return HandleResult(result,nameof(GetVariantById));
        }

      
        [HttpPost()]
        [Authorize(Roles = "Admin,SuperAdmin")]
        public async Task<ActionResult<ApiResponse<List<ProductVariantDto>>>> CreateVariants(int productId, [FromBody] List<CreateProductVariantDto> dto)
        {
            if (!ModelState.IsValid)
            {
                var errors = GetModelErrors();
                _logger.LogError($"Validation Errors: {errors}");
                return BadRequest(ApiResponse<ProductVariantDto>.CreateErrorResponse("Invalid variant data", new ErrorResponse("Invalid data", errors)));
            }
            
            var userId = GetUserId();
            var result = await _variantService.AddVariantsAsync(productId, dto, userId);
            return HandleResult(result, nameof(CreateVariants));
        }

        // PUT api/products/{productId}/variants/{id}
        [HttpPut("{id}")]
        [Authorize(Roles = "Admin,SuperAdmin")]
        public async Task<ActionResult<ApiResponse<ProductVariantDto>>> UpdateVariant(int productId, int id, [FromBody] UpdateProductVariantDto dto)
        {
            if (!ModelState.IsValid)
            {
                var errors =GetModelErrors();
                _logger.LogError($"Validation Errors: {errors}");
                return BadRequest(ApiResponse<ProductVariantDto>.CreateErrorResponse("Invalid variant data", new ErrorResponse("Invalid data", errors)));
            }
            
            var userId = GetUserId();
            var result = await _variantService.UpdateVariantAsync(id, dto, userId);
            return HandleResult(result,nameof(UpdateVariant));
        }

        // DELETE api/products/{productId}/variants/{id}
        [HttpDelete("{id}")]
        [Authorize(Roles = "Admin,SuperAdmin")]
        public async Task<ActionResult<ApiResponse<bool>>> DeleteVariant(int productId, int id)
        {
            var userId = GetUserId();
            var result = await _variantService.DeleteVariantAsync(id, userId);
            return HandleResult<bool>(result,nameof(DeleteVariant));
        }

        // PATCH api/products/{productId}/variants/{id}/quantity/add
        [HttpPatch("{id}/quantity/add")]
        [Authorize(Roles = "Admin,SuperAdmin")]
        public async Task<ActionResult<ApiResponse<bool>>> AddVariantQuantity(int productId, int id, [FromQuery] int quantity)
        {
            if (!ModelState.IsValid)
            {
                var errors = GetModelErrors();
                _logger.LogError($"Validation Errors: {errors}");
                return BadRequest(ApiResponse<bool>.CreateErrorResponse("Invalid quantity data", new ErrorResponse("Invalid data", errors)));
            }
            
            if (quantity <= 0)
            {
                return BadRequest(ApiResponse<bool>.CreateErrorResponse("Invalid quantity", new ErrorResponse("Invalid data", "Quantity must be greater than 0")));
            }
            
            var userId = GetUserId();
            var result = await _variantService.AddVariantQuantityAsync(id, quantity, userId);
            return HandleResult(result,nameof(AddVariantQuantity));
        }

        // PATCH api/products/{productId}/variants/{id}/quantity/remove
        [HttpPatch("{id}/quantity/remove")]
        [Authorize(Roles = "Admin,SuperAdmin")]
        public async Task<ActionResult<ApiResponse<bool>>> RemoveVariantQuantity(int productId, int id, [FromQuery] int quantity)
        {
            if (!ModelState.IsValid)
            {
                var errors = GetModelErrors();
                _logger.LogError($"Validation Errors: {errors}");
                return BadRequest(ApiResponse<bool>.CreateErrorResponse("Invalid quantity data", new ErrorResponse("Invalid data", errors)));
            }
            
            if (quantity <= 0)
            {
                return BadRequest(ApiResponse<bool>.CreateErrorResponse("Invalid quantity", new ErrorResponse("Invalid data", "Quantity must be greater than 0")));
            }
            
            var userId = GetUserId();
            var result = await _variantService.RemoveVariantQuantityAsync(id, quantity, userId);
            return HandleResult<bool>(result);
        }

        // PATCH api/products/{productId}/variants/{id}/activate
        [HttpPatch("{id}/activate")]
        [Authorize(Roles = "Admin,SuperAdmin")]
        public async Task<ActionResult<ApiResponse<bool>>> ActivateVariant(int productId, int id)
        {
            var userId = GetUserId();
            var result = await _variantService.ActivateVariantAsync(id, userId);
            return HandleResult<bool>(result);
        }

        // PATCH api/products/{productId}/variants/{id}/deactivate
        [HttpPatch("{id}/deactivate")]
        [Authorize(Roles = "Admin,SuperAdmin")]
        public async Task<ActionResult<ApiResponse<bool>>> DeactivateVariant(int productId, int id)
        {
            var userId = GetUserId();
            var result = await _variantService.DeactivateVariantAsync(id, userId);
            return HandleResult<bool>(result);
        }

        // PATCH api/products/{productId}/variants/{id}/restore
        [HttpPatch("{id}/restore")]
        [Authorize(Roles = "Admin,SuperAdmin")]
        public async Task<ActionResult<ApiResponse<bool>>> RestoreVariant(int productId, int id)
        {
            var userId = GetUserId();
            var result = await _variantService.RestoreVariantAsync(id, userId);
            return HandleResult<bool>(result);
        }

        // GET api/products/{productId}/variants/search
        [HttpGet()]
        [AllowAnonymous]
        public async Task<ActionResult<ApiResponse<List<ProductVariantDto>>>> SearchVariants(int productId, [FromQuery] string? color = null, [FromQuery] int? length = null, [FromQuery] int? waist = null, [FromQuery] VariantSize? size = null, [FromQuery] bool? isActive = null, [FromQuery] bool? deletedOnly = null)
        {
            bool isAdmin = HasManagementRole();
            
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