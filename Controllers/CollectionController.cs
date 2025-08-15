using E_Commerce.DtoModels.CategoryDtos;
using E_Commerce.DtoModels.CollectionDtos;
using E_Commerce.DtoModels.ImagesDtos;
using E_Commerce.DtoModels.Responses;
using E_Commerce.ErrorHnadling;
using E_Commerce.Interfaces;
using E_Commerce.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using System.Threading.Tasks;

namespace E_Commerce.Controllers
{
    [ApiController]
    [Route("api/[controller]")]

    public class CollectionController : ControllerBase
    {
        private readonly ICollectionServices _collectionServices;
        private readonly ILogger<CollectionController> _logger;

        public CollectionController(ICollectionServices collectionServices, ILogger<CollectionController> logger)
        {
            _collectionServices = collectionServices ?? throw new ArgumentNullException(nameof(collectionServices));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        private string GetUserId() => User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? string.Empty;
        private string GetUserRole() => User.FindFirst(ClaimTypes.Role)?.Value ?? "User";

        private ActionResult<ApiResponse<T>> HandleResult<T>(Result<T> result, string? actionName = null, object? routeValues = null)
        {
            var apiResponse = result.Success
                ? ApiResponse<T>.CreateSuccessResponse(result.Message, result.Data, result.StatusCode, warnings: result.Warnings)
                : ApiResponse<T>.CreateErrorResponse(result.Message, new ErrorResponse("Error", result.Message), result.StatusCode, warnings: result.Warnings);

            return result.StatusCode switch
            {
                200 => Ok(apiResponse),
                201 => actionName != null 
                    ? CreatedAtAction(actionName, routeValues, apiResponse) 
                    : StatusCode(201, apiResponse),
                400 => BadRequest(apiResponse),
                401 => Unauthorized(apiResponse),
                403 => Forbid(),
                404 => NotFound(apiResponse),
                409 => Conflict(apiResponse),
                _ => StatusCode(result.StatusCode, apiResponse)
            };
        }

        /// <summary>
        /// Get collection by ID (unified endpoint that checks user role)
        /// </summary>
        [HttpGet("{id}")]
        [AllowAnonymous]
        public async Task<ActionResult<ApiResponse<CollectionDto>>> GetCollectionById(int id)
        {
            _logger.LogInformation($"Executing {nameof(GetCollectionById)} for ID: {id}");
            
            var role = GetUserRole();
            var isAdmin = role == "Admin";
            
            // For non-admin users, only show active and non-deleted collections
            var result = await _collectionServices.GetCollectionByIdAsync(
                id, 
                isAdmin ? null : true, 
                isAdmin ? null : false);
                
            return HandleResult(result, nameof(GetCollectionById), id);
        }

        /// <summary>
        /// Get all collections (unified endpoint that checks user role)
        /// </summary>
        [HttpGet]
        [AllowAnonymous]
        public async Task<ActionResult<ApiResponse<List<CollectionSummaryDto>>>> GetCollections(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 10,
            [FromQuery] bool? isActive = null,
            [FromQuery] bool? isDeleted = null)
        {
            _logger.LogInformation($"Executing {nameof(GetCollections)} with pagination: page {page}, size {pageSize}");
            
            var role = GetUserRole();
            var isAdmin = role == "Admin";
            
            // For non-admin users, only show active and non-deleted collections
            var activeFilter = isAdmin ? isActive : true;
            var deletedFilter = isAdmin ? isDeleted : false;
            
            var result = await _collectionServices.SearchCollectionsAsync(
                null, 
                activeFilter, 
                deletedFilter, 
                page, 
                pageSize);
                
            return HandleResult(result, nameof(GetCollections));
        }

        /// <summary>
        /// Search collections (unified endpoint that checks user role)
        /// </summary>
        [HttpGet("search")]
        [AllowAnonymous]
        public async Task<ActionResult<ApiResponse<List<CollectionSummaryDto>>>> SearchCollections(
            [FromQuery] string searchTerm,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 10,
            [FromQuery] bool? isActive = null,
            [FromQuery] bool? isDeleted = null)
        {
            _logger.LogInformation($"Executing {nameof(SearchCollections)} for term: {searchTerm} with pagination: page {page}, size {pageSize}");
            
            var role = GetUserRole();
            var isAdmin = role == "Admin";
            
            // For non-admin users, only show active and non-deleted collections
            var activeFilter = isAdmin ? isActive : true;
            var deletedFilter = isAdmin ? isDeleted : false;
            
            var result = await _collectionServices.SearchCollectionsAsync(
                searchTerm, 
                activeFilter, 
                deletedFilter, 
                page, 
                pageSize);
                
            return HandleResult(result, nameof(SearchCollections));
        }

        private List<string> GetModelErrors()
        {
            return ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).ToList();
        }

        // Admin-only endpoints (kept as-is since they require admin role)
        /// <summary>
        /// Create collection (Admin only)
        /// </summary>
        [HttpPost]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult<ApiResponse<CollectionSummaryDto>>> CreateCollection([FromBody] CreateCollectionDto collectionDto)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    var errors = GetModelErrors();
                    _logger.LogWarning($"ModelState errors: {string.Join(", ", errors)}");
                    return BadRequest(ApiResponse<CollectionDto>.CreateErrorResponse("Invalid Data", new ErrorResponse("Invalid Data", errors), 400));
                }

                _logger.LogInformation($"Executing CreateCollection: {collectionDto.Name}");
                var userid = HttpContext.Items["UserId"].ToString();
                var result = await _collectionServices.CreateCollectionAsync(collectionDto, userid);
                return HandleResult(result, nameof(CreateCollection));
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error in CreateCollection: {ex.Message}");
                return StatusCode(500, ApiResponse<CollectionDto>.CreateErrorResponse("Server Error", new ErrorResponse("Server Error", "An error occurred while creating the collection"), 500));
            }
        }

        /// <summary>
        /// Update collection (Admin only)
        /// </summary>
        [HttpPut("{id}")]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult<ApiResponse<CollectionSummaryDto>>> UpdateCollection(
            int id,
            [FromBody] UpdateCollectionDto collectionDto)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    var errors = GetModelErrors();
                    _logger.LogWarning($"ModelState errors: {string.Join(", ", errors)}");
                    return BadRequest(ApiResponse<CollectionDto>.CreateErrorResponse("Invalid Data", new ErrorResponse("Invalid Data", errors), 400));
                }

                _logger.LogInformation($"Executing UpdateCollection for ID: {id}");
                var userid = HttpContext.Items["UserId"].ToString();
                var result = await _collectionServices.UpdateCollectionAsync(id, collectionDto, userid);
                return HandleResult(result, nameof(UpdateCollection), id);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error in UpdateCollection: {ex.Message}");
                return StatusCode(500, ApiResponse<CollectionDto>.CreateErrorResponse("Server Error", new ErrorResponse("Server Error", "An error occurred while updating the collection"), 500));
            }
        }

        /// <summary>
        /// Delete collection (Admin only)
        /// </summary>
        [HttpDelete("{id}")]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult<ApiResponse<bool>>> DeleteCollection(int id)
        {
            if (id <= 0)
            {
                return BadRequest(ApiResponse<bool>.CreateErrorResponse(
                    "Invalid ID",
                    new ErrorResponse("Validation", new List<string> { "ID must be greater than 0" }),
                    400
                ));
            }
            try
            {
                _logger.LogInformation($"Executing {nameof(DeleteCollection)} for ID: {id}");
                var userid = HttpContext.Items["UserId"].ToString();
                var result = await _collectionServices.DeleteCollectionAsync(id, userid);
                return HandleResult(result, nameof(DeleteCollection), id);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error in {nameof(DeleteCollection)}: {ex.Message}");
                return StatusCode(500, ApiResponse<string>.CreateErrorResponse("Server Error", new ErrorResponse("Server Error", "An error occurred while deleting the collection"), 500));
            }
        }

        /// <summary>
        /// Activate a collection (Admin only)
        /// </summary>
        [HttpPatch("{id}/activate")]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult<ApiResponse<bool>>> ActivateCollection(int id)
        {
            if (id <= 0)
            {
                return BadRequest(ApiResponse<bool>.CreateErrorResponse(
                    "Invalid ID",
                    new ErrorResponse("Validation", new List<string> { "ID must be greater than 0" }),
                    400
                ));
            }
            var userId = GetUserId();
            var result = await _collectionServices.ActivateCollectionAsync(id, userId);
            return HandleResult(result);
        }

        /// <summary>
        /// Deactivate a collection (Admin only)
        /// </summary>
        [HttpPatch("{id}/deactivate")]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult<ApiResponse<bool>>> DeactivateCollection(int id)
        {
            if (id <= 0)
            {
                return BadRequest(ApiResponse<bool>.CreateErrorResponse(
                    "Invalid ID",
                    new ErrorResponse("Validation", new List<string> { "ID must be greater than 0" }),
                    400
                ));
            }
            var userId = GetUserId();
            var result = await _collectionServices.DeactivateCollectionAsync(id, userId);
            return HandleResult(result);
        }

        /// <summary>
        /// Add images to a collection (Admin only)
        /// </summary>
        [HttpPost("{id}/images")]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult<ApiResponse<List<ImageDto>>>> AddImagesToCollection(
            int id,
            [FromForm] AddImagesDto images)
        {
            if (id <= 0)
            {
                return BadRequest(ApiResponse<List<ImageDto>>.CreateErrorResponse(
                    "Invalid ID",
                    new ErrorResponse("Validation", new List<string> { "ID must be greater than 0" }),
                    400
                ));
            }
            if (!ModelState.IsValid)
            {
                var errors = GetModelErrors();
                _logger.LogWarning($"ModelState errors: {string.Join(", ", errors)}");
                return BadRequest(ApiResponse<CollectionDto>.CreateErrorResponse("Invalid Data", new ErrorResponse("Invalid Data", errors), 400));
            }
            var userId = GetUserId();
            var result = await _collectionServices.AddImagesToCollectionAsync(id, images.Images, userId);
            return HandleResult(result);
        }

        /// <summary>
        /// Add main image to a collection (Admin only)
        /// </summary>
        [HttpPost("{id}/main-image")]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult<ApiResponse<ImageDto>>> AddMainImageToCollection(
            int id,
           [FromForm] AddMainImageDto image)
        {
            if (id <= 0)
            {
                return BadRequest(ApiResponse<ImageDto>.CreateErrorResponse(
                    "Invalid ID",
                    new ErrorResponse("Validation", new List<string> { "ID must be greater than 0" }),
                    400
                ));
            }
            if (!ModelState.IsValid)
            {
                var errors = GetModelErrors();
                _logger.LogWarning($"ModelState errors: {string.Join(", ", errors)}");
                return BadRequest(ApiResponse<CollectionDto>.CreateErrorResponse("Invalid Data", new ErrorResponse("Invalid Data", errors), 400));
            }
            var userId = GetUserId();
            var result = await _collectionServices.AddMainImageToCollectionAsync(id, image.Image, userId);
            return HandleResult(result);
        }

        /// <summary>
        /// Remove image from a collection (Admin only)
        /// </summary>
        [HttpDelete("{id}/images/{imageId}")]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult<ApiResponse<bool>>> RemoveImageFromCollection(
            int id,
            int imageId)
        {
            if (id <= 0 || imageId <= 0)
            {
                return BadRequest(ApiResponse<bool>.CreateErrorResponse(
                    "Invalid ID",
                    new ErrorResponse("Validation", new List<string> { "ID must be greater than 0" }),
                    400
                ));
            }
            if (!ModelState.IsValid)
            {
                var errors = GetModelErrors();
                _logger.LogWarning($"ModelState errors: {string.Join(", ", errors)}");
                return BadRequest(ApiResponse<CollectionDto>.CreateErrorResponse("Invalid Data", new ErrorResponse("Invalid Data", errors), 400));
            }
            var userId = GetUserId();
            var result = await _collectionServices.RemoveImageFromCollectionAsync(id, imageId, userId);
            return HandleResult(result);
        }

        /// <summary>
        /// Add products to collection (Admin only)
        /// </summary>
        [HttpPost("{id}/products")]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult<ApiResponse<bool>>> AddProductsToCollection(
            int id,
            [FromForm] AddProductsToCollectionDto productsDto)
        {
            if (id <= 0)
            {
                return BadRequest(ApiResponse<bool>.CreateErrorResponse(
                    "Invalid ID",
                    new ErrorResponse("Validation", new List<string> { "ID must be greater than 0" }),
                    400
                ));
            }
            try
            {
                if (!ModelState.IsValid)
                {
                    var errors = GetModelErrors();
                    _logger.LogWarning($"ModelState errors: {string.Join(", ", errors)}");
                    return BadRequest(ApiResponse<bool>.CreateErrorResponse("Invalid Data", new ErrorResponse("Invalid Data", errors), 400));
                }

                _logger.LogInformation($"Executing {nameof(AddProductsToCollection)} for collection ID: {id}");
                var userid = HttpContext.Items["UserId"].ToString();
                var result = await _collectionServices.AddProductsToCollectionAsync(id, productsDto, userid);
                return HandleResult(result, nameof(AddProductsToCollection), id);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error in {nameof(AddProductsToCollection)}: {ex.Message}");
                return StatusCode(500, ApiResponse<bool>.CreateErrorResponse("Server Error", new ErrorResponse("Server Error", "An error occurred while adding products to collection"), 500));
            }
        }

        /// <summary>
        /// Remove products from collection (Admin only)
        /// </summary>
        [HttpDelete("{id}/products")]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult<ApiResponse<bool>>> RemoveProductsFromCollection(
            int id,
            [FromBody] RemoveProductsFromCollectionDto productsDto)
        {
            if (id <= 0)
            {
                return BadRequest(ApiResponse<bool>.CreateErrorResponse(
                    "Invalid ID",
                    new ErrorResponse("Validation", new List<string> { "ID must be greater than 0" }),
                    400
                ));
            }
            try
            {
                if (!ModelState.IsValid)
                {
                    var errors = GetModelErrors();
                    _logger.LogWarning($"ModelState errors: {string.Join(", ", errors)}");
                    return BadRequest(ApiResponse<string>.CreateErrorResponse("Invalid Data", new ErrorResponse("Invalid Data", errors), 400));
                }

                _logger.LogInformation($"Executing {nameof(RemoveProductsFromCollection)} for collection ID: {id}");
                var userid = HttpContext.Items["UserId"].ToString();
                var result = await _collectionServices.RemoveProductsFromCollectionAsync(id, productsDto, userid);
                return HandleResult(result, nameof(RemoveProductsFromCollection), id);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error in {nameof(RemoveProductsFromCollection)}: {ex.Message}");
                return StatusCode(500, ApiResponse<string>.CreateErrorResponse("Server Error", new ErrorResponse("Server Error", "An error occurred while removing products from collection"), 500));
            }
        }

        /// <summary>
        /// Update collection display order (Admin only)
        /// </summary>
        [HttpPatch("{id}/display-order")]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult<ApiResponse<bool>>> UpdateCollectionDisplayOrder(
            int id,
            [FromBody] int displayOrder)
        {
            if (id <= 0)
            {
                return BadRequest(ApiResponse<bool>.CreateErrorResponse(
                    "Invalid ID",
                    new ErrorResponse("Validation", new List<string> { "ID must be greater than 0" }),
                    400
                ));
            }
            try
            {
                _logger.LogInformation($"Executing {nameof(UpdateCollectionDisplayOrder)} for ID: {id}, order: {displayOrder}");
                var userid = HttpContext.Items["UserId"].ToString();
                var result = await _collectionServices.UpdateCollectionDisplayOrderAsync(id, displayOrder, userid);
                return HandleResult(result, nameof(UpdateCollectionDisplayOrder), id);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error in {nameof(UpdateCollectionDisplayOrder)}: {ex.Message}");
                return StatusCode(500, ApiResponse<bool>.CreateErrorResponse("Server Error", new ErrorResponse("Server Error", "An error occurred while updating collection display order"), 500));
            }
        }
    }
}