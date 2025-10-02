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
    public class CollectionController : BaseController
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

        

       

    

     
       

        [HttpPatch("activate/{id}")]
        [Authorize(Roles ="Admin,SuperAdmin")]
		public async Task<ActionResult<ApiResponse<bool>>> ActiveAsync(int id)
		{
			var userid = HttpContext?.Items["UserId"]?.ToString();
			var response = await _collectionServices.ActivateCollectionAsync(id, userid);
			return HandleResult  (response,nameof(ActiveAsync),id);
        }
        [HttpPatch("dactivate/{id}")]
        [Authorize(Roles = "Admin,SuperAdmin")]
        public async Task<ActionResult<ApiResponse<bool>>> DeactiveAsync(int id)
		{
			var userid = HttpContext?.Items["UserId"]?.ToString();
			var response = await _collectionServices.DeactivateCollectionAsync(id, userid);
			return HandleResult  (response,nameof(ActiveAsync),id);
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
            
          
            var isAdmin = HasManagementRole();
          
            var result = await _collectionServices.GetCollectionByIdAsync(
                id, 
                isAdmin ? null : true, 
                isAdmin ? null : false,
                isAdmin
            );
                
            return HandleResult(result, nameof(GetCollectionById),  id );
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
            

            var isAdmin = HasManagementRole();

            var activeFilter = isAdmin ? isActive : true;
            var deletedFilter = isAdmin ? isDeleted : false;
            
            var result = await _collectionServices.SearchCollectionsAsync(
                searchTerm, 
                activeFilter, 
                deletedFilter, 
                page, 
                pageSize,
                isAdmin
            );
                
            return HandleResult(result, nameof(SearchCollections));
        }
        #endregion

        #region Admin CRUD Operations
        /// <summary>
        /// Create collection (Admin only)
        /// </summary>
        [HttpPost]
        [Authorize(Roles = "Admin,SuperAdmin")]
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
                var userid = GetUserId();
                var result = await _collectionServices.CreateCollectionAsync(collectionDto, userid);
                return HandleResult(result, nameof(CreateCollection));
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
        [Authorize(Roles = "Admin,SuperAdmin")]
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
                var userid = GetUserId();
                var result = await _collectionServices.UpdateCollectionAsync(
                    id,
                    collectionDto,
                    userid
                );
                return HandleResult(result, nameof(UpdateCollection),  id );
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
        [Authorize(Roles = "Admin,SuperAdmin")]
        public async Task<ActionResult<ApiResponse<bool>>> DeleteCollection(int id)
        {
            if (id <= 0)
            {
                return CreateErrorResponse<bool>("ID must be greater than 0", 400);
            }

            try
            {
                _logger.LogInformation($"Executing {nameof(DeleteCollection)} for ID: {id}");
                var userid = GetUserId();
                var result = await _collectionServices.DeleteCollectionAsync(id, userid);
                return HandleResult(result, nameof(DeleteCollection), id);
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
        [AllowAnonymous()]
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
                var isAdmin = HasManagementRole();

                var collection = await _collectionServices.GetCollectionByIdAsync(id, null, null, isAdmin);
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
        [Authorize(Roles = "Admin,SuperAdmin")]
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
                return HandleResult(result);
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
        [Authorize(Roles = "Admin,SuperAdmin")]
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
                return HandleResult(result);
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
        [Authorize(Roles = "Admin,SuperAdmin")]
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
                return HandleResult(result);
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
        [AllowAnonymous()]
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
               
                var isAdmin = HasManagementRole();

                var result = await _collectionServices.GetCollectionByIdAsync(
                    id,
                    isAdmin ? null : true,
                    isAdmin ? null : false,
                    isAdmin
                );
                if (!result.Success)
                {
                    return CreateErrorResponse<List<ProductDto>>(result.Message, result.StatusCode);
                }

                var products = result.Data?.Products?.ToList() ?? new List<ProductDto>();
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
        [Authorize(Roles = "Admin,SuperAdmin")]
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
                var userid = GetUserId();
                var result = await _collectionServices.AddProductsToCollectionAsync(
                    id,
                    productsDto,
                    userid
                );
                return HandleResult(result, nameof(AddProductsToCollection), id);
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
        [Authorize(Roles = "Admin,SuperAdmin")]
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
                var userid = GetUserId();
                var result = await _collectionServices.RemoveProductsFromCollectionAsync(
                    id,
                    productsDto,
                    userid
                );
                return HandleResult(result, nameof(RemoveProductsFromCollection), id);
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
