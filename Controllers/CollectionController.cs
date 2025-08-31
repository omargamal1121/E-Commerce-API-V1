using System.Security.Claims;
using System.Threading.Tasks;
using E_Commerce.DtoModels.CategoryDtos;
using E_Commerce.DtoModels.CollectionDtos;
using E_Commerce.DtoModels.ImagesDtos;
using E_Commerce.DtoModels.ProductDtos;
using E_Commerce.DtoModels.Responses;
using E_Commerce.ErrorHnadling;
using E_Commerce.Interfaces;
using E_Commerce.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace E_Commerce.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class CollectionController : ControllerBase
    {
        private readonly ICollectionServices _collectionServices;
        private readonly ILogger<CollectionController> _logger;

        public CollectionController(
            ICollectionServices collectionServices,
            ILogger<CollectionController> logger
        )
        {
            _collectionServices =
                collectionServices ?? throw new ArgumentNullException(nameof(collectionServices));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        private string GetUserId() =>
            HttpContext.Items?["UserId"].ToString()??string.Empty;

        private string GetUserRole() => User.FindFirst(ClaimTypes.Role)?.Value ?? "User";

        private ActionResult<ApiResponse<T>> CreateResponse<T>(
            Result<T> result,
            string? actionName = null,
            object? routeValues = null
        )
        {
            if (!result.Success)
            {
                var errorResponse = ApiResponse<T>.CreateErrorResponse(
                    result.Message,
                    new ErrorResponse("Error", result.Message),
                    result.StatusCode,
                    warnings: result.Warnings
                );
                return StatusCode(result.StatusCode, errorResponse);
            }

            var successResponse = ApiResponse<T>.CreateSuccessResponse(
                result.Message,
                result.Data,
                result.StatusCode,
                warnings: result.Warnings
            );

            return result.StatusCode switch
            {
                200 => Ok(successResponse),
                201 => actionName != null 
                    ? CreatedAtAction(actionName, routeValues, successResponse) 
                    : StatusCode(201, successResponse),
                400 => BadRequest(successResponse),
                401 => Unauthorized(successResponse),
                403 => Forbid(),
                404 => NotFound(successResponse),
                409 => Conflict(successResponse),
                _ => StatusCode(result.StatusCode, successResponse),
            };
        }

        private ActionResult<ApiResponse<T>> CreateErrorResponse<T>(
            string message,
            int statusCode,
            List<string>? errors = null)
        {
            var errorResponse = ApiResponse<T>.CreateErrorResponse(
                message,
                new ErrorResponse("Error", errors ?? new List<string> { message }),
                statusCode
            );
            return StatusCode(statusCode, errorResponse);
        }

        private List<string> GetModelErrors()
        {
            return ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).ToList();
        }

        #region Public Read Operations (Anonymous)
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
                isAdmin ? null : false
            );
                
            return CreateResponse(result, nameof(GetCollectionById), new { id });
        }

   
        /// <summary>
        /// Search collections (unified endpoint that checks user role)
        /// </summary>
        [HttpGet()]
        [AllowAnonymous]
        public async Task<ActionResult<ApiResponse<List<CollectionSummaryDto>>>> SearchCollections(
            [FromQuery] string? searchTerm,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 10,
            [FromQuery] bool? isActive = null,
            [FromQuery] bool? isDeleted = null
        )
        {
            _logger.LogInformation(
                $"Executing {nameof(SearchCollections)} for term: {searchTerm} with pagination: page {page}, size {pageSize}"
            );
            
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
                pageSize
            );
                
            return CreateResponse(result, nameof(SearchCollections));
        }
        #endregion

        #region Admin CRUD Operations
        /// <summary>
        /// Create collection (Admin only)
        /// </summary>
        [HttpPost]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult<ApiResponse<CollectionSummaryDto>>> CreateCollection(
            [FromBody] CreateCollectionDto collectionDto
        )
        {
            if (!ModelState.IsValid)
            {
                var errors = GetModelErrors();
                _logger.LogWarning($"ModelState errors: {string.Join(", ", errors)}");
                return CreateErrorResponse<CollectionSummaryDto>("Invalid Data", 400, errors);
            }

            try
            {
                _logger.LogInformation($"Executing CreateCollection: {collectionDto.Name}");
                var userid = HttpContext.Items["UserId"].ToString();
                var result = await _collectionServices.CreateCollectionAsync(collectionDto, userid);
                return CreateResponse(result, nameof(CreateCollection));
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error in CreateCollection: {ex.Message}");
                return CreateErrorResponse<CollectionSummaryDto>(
                    "An error occurred while creating the collection", 500);
            }
        }

        /// <summary>
        /// Update collection (Admin only) - Handles all updates including status and display order
        /// </summary>
        [HttpPut("{id}")]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult<ApiResponse<CollectionSummaryDto>>> UpdateCollection(
            int id,
            [FromBody] UpdateCollectionDto collectionDto
        )
        {
            if (!ModelState.IsValid)
            {
                var errors = GetModelErrors();
                _logger.LogWarning($"ModelState errors: {string.Join(", ", errors)}");
                return CreateErrorResponse<CollectionSummaryDto>("Invalid Data", 400, errors);
            }

            try
            {
                _logger.LogInformation($"Executing UpdateCollection for ID: {id}");
                var userid = HttpContext.Items["UserId"].ToString();
                var result = await _collectionServices.UpdateCollectionAsync(
                    id,
                    collectionDto,
                    userid
                );
                return CreateResponse(result, nameof(UpdateCollection), new { id });
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error in UpdateCollection: {ex.Message}");
                return CreateErrorResponse<CollectionSummaryDto>(
                    "An error occurred while updating the collection", 500);
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
                return CreateErrorResponse<bool>("ID must be greater than 0", 400);
            }

            try
            {
                _logger.LogInformation($"Executing {nameof(DeleteCollection)} for ID: {id}");
                var userid = HttpContext.Items["UserId"].ToString();
                var result = await _collectionServices.DeleteCollectionAsync(id, userid);
                return CreateResponse(result, nameof(DeleteCollection), new { id });
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error in {nameof(DeleteCollection)}: {ex.Message}");
                return CreateErrorResponse<bool>(
                    "An error occurred while deleting the collection", 500);
            }
        }
        #endregion

        #region Collection Images Management
        /// <summary>
        /// Get collection images (Admin only)
        /// </summary>
        [HttpGet("{id}/images")]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult<ApiResponse<List<ImageDto>>>> GetCollectionImages(int id)
        {
            if (id <= 0)
            {
                return CreateErrorResponse<List<ImageDto>>("ID must be greater than 0", 400);
            }

            try
            {
                _logger.LogInformation(
                    $"Executing {nameof(GetCollectionImages)} for collection ID: {id}"
                );
                var collection = await _collectionServices.GetCollectionByIdAsync(id, null, null);
                if (!collection.Success)
                {
                    return CreateErrorResponse<List<ImageDto>>(collection.Message, collection.StatusCode);
                }

                var images = collection.Data?.Images?.ToList() ?? new List<ImageDto>();
                var successResponse = ApiResponse<List<ImageDto>>.CreateSuccessResponse(
                    "Collection images retrieved successfully",
                    images,
                    200
                );
                return Ok(successResponse);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error in {nameof(GetCollectionImages)}: {ex.Message}");
                return CreateErrorResponse<List<ImageDto>>(
                    "An error occurred while retrieving collection images", 500);
            }
        }

        /// <summary>
        /// Add images to a collection (Admin only)
        /// </summary>
        [HttpPost("{id}/images")]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult<ApiResponse<List<ImageDto>>>> AddImagesToCollection(
            int id,
            [FromForm] AddImagesDto images
        )
        {
            if (id <= 0)
            {
                return CreateErrorResponse<List<ImageDto>>("ID must be greater than 0", 400);
            }

            if (!ModelState.IsValid)
            {
                var errors = GetModelErrors();
                _logger.LogWarning($"ModelState errors: {string.Join(", ", errors)}");
                return CreateErrorResponse<List<ImageDto>>("Invalid Data", 400, errors);
            }

            try
            {
                var userId = GetUserId();
                var result = await _collectionServices.AddImagesToCollectionAsync(
                    id,
                    images.Images,
                    userId
                );
                return CreateResponse(result);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error in {nameof(AddImagesToCollection)}: {ex.Message}");
                return CreateErrorResponse<List<ImageDto>>(
                    "An error occurred while adding images to collection", 500);
            }
        }

        /// <summary>
        /// Set main image for collection (Admin only) - RESTful PUT operation
        /// </summary>
        [HttpPut("{id}/main-image")]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult<ApiResponse<ImageDto>>> SetMainImage(
            int id,
            [FromForm] AddMainImageDto image
        )
        {
            if (id <= 0)
            {
                return CreateErrorResponse<ImageDto>("ID must be greater than 0", 400);
            }

            if (!ModelState.IsValid)
            {
                var errors = GetModelErrors();
                _logger.LogWarning($"ModelState errors: {string.Join(", ", errors)}");
                return CreateErrorResponse<ImageDto>("Invalid Data", 400, errors);
            }

            try
            {
                var userId = GetUserId();
                var result = await _collectionServices.AddMainImageToCollectionAsync(
                    id,
                    image.Image,
                    userId
                );
                return CreateResponse(result);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error in {nameof(SetMainImage)}: {ex.Message}");
                return CreateErrorResponse<ImageDto>(
                    "An error occurred while setting main image", 500);
            }
        }

        /// <summary>
        /// Remove image from a collection (Admin only)
        /// </summary>
        [HttpDelete("{id}/images/{imageId}")]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult<ApiResponse<bool>>> RemoveImageFromCollection(
            int id,
            int imageId
        )
        {
            if (id <= 0 || imageId <= 0)
            {
                return CreateErrorResponse<bool>("ID must be greater than 0", 400);
            }

            try
            {
                var userId = GetUserId();
                var result = await _collectionServices.RemoveImageFromCollectionAsync(
                    id,
                    imageId,
                    userId
                );
                return CreateResponse(result);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error in {nameof(RemoveImageFromCollection)}: {ex.Message}");
                return CreateErrorResponse<bool>(
                    "An error occurred while removing image from collection", 500);
            }
        }
        #endregion

        #region Collection Products Management
        /// <summary>
        /// Get collection products (Admin only)
        /// </summary>
        [HttpGet("{id}/products")]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult<ApiResponse<List<ProductDto>>>> GetCollectionProducts(int id)
        {
            if (id <= 0)
            {
                return CreateErrorResponse<List<ProductDto>>("ID must be greater than 0", 400);
            }

            try
            {
                _logger.LogInformation(
                    $"Executing {nameof(GetCollectionProducts)} for collection ID: {id}"
                );
                var collection = await _collectionServices.GetCollectionByIdAsync(id, null, null);
                if (!collection.Success)
                {
                    return CreateErrorResponse<List<ProductDto>>(collection.Message, collection.StatusCode);
                }

                var products = collection.Data?.Products?.ToList() ?? new List<ProductDto>();
                var successResponse = ApiResponse<List<ProductDto>>.CreateSuccessResponse(
                    "Collection products retrieved successfully",
                    products,
                    200
                );
                return Ok(successResponse);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error in {nameof(GetCollectionProducts)}: {ex.Message}");
                return CreateErrorResponse<List<ProductDto>>(
                    "An error occurred while retrieving collection products", 500);
            }
        }

        /// <summary>
        /// Add products to collection (Admin only)
        /// </summary>
        [HttpPost("{id}/products")]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult<ApiResponse<bool>>> AddProductsToCollection(
            int id,
            [FromForm] AddProductsToCollectionDto productsDto
        )
        {
            if (id <= 0)
            {
                return CreateErrorResponse<bool>("ID must be greater than 0", 400);
            }

            if (!ModelState.IsValid)
            {
                var errors = GetModelErrors();
                _logger.LogWarning($"ModelState errors: {string.Join(", ", errors)}");
                return CreateErrorResponse<bool>("Invalid Data", 400, errors);
            }

            try
            {
                _logger.LogInformation(
                    $"Executing {nameof(AddProductsToCollection)} for collection ID: {id}"
                );
                var userid = HttpContext.Items["UserId"].ToString();
                var result = await _collectionServices.AddProductsToCollectionAsync(
                    id,
                    productsDto,
                    userid
                );
                return CreateResponse(result, nameof(AddProductsToCollection), new { id });
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error in {nameof(AddProductsToCollection)}: {ex.Message}");
                return CreateErrorResponse<bool>(
                    "An error occurred while adding products to collection", 500);
            }
        }

        /// <summary>
        /// Remove products from collection (Admin only)
        /// </summary>
        [HttpDelete("{id}/products")]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult<ApiResponse<bool>>> RemoveProductsFromCollection(
            int id,
            [FromBody] RemoveProductsFromCollectionDto productsDto
        )
        {
            if (id <= 0)
            {
                return CreateErrorResponse<bool>("ID must be greater than 0", 400);
            }

            if (!ModelState.IsValid)
            {
                var errors = GetModelErrors();
                _logger.LogWarning($"ModelState errors: {string.Join(", ", errors)}");
                return CreateErrorResponse<bool>("Invalid Data", 400, errors);
            }

            try
            {
                _logger.LogInformation(
                    $"Executing {nameof(RemoveProductsFromCollection)} for collection ID: {id}"
                );
                var userid = HttpContext.Items["UserId"].ToString();
                var result = await _collectionServices.RemoveProductsFromCollectionAsync(
                    id,
                    productsDto,
                    userid
                );
                return CreateResponse(result, nameof(RemoveProductsFromCollection), new { id });
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error in {nameof(RemoveProductsFromCollection)}: {ex.Message}");
                return CreateErrorResponse<bool>(
                    "An error occurred while removing products from collection", 500);
            }
        }
        #endregion
    }
}
