using AutoMapper;
using E_Commerce.DtoModels;
using E_Commerce.DtoModels.CategoryDtos;
using E_Commerce.DtoModels.ImagesDtos;
using E_Commerce.DtoModels.Responses;
using E_Commerce.DtoModels.Shared;
using E_Commerce.ErrorHnadling;
using E_Commerce.Interfaces;
using E_Commerce.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using System.ComponentModel.DataAnnotations;
using E_Commerce.DtoModels.SubCategorydto;

namespace E_Commerce.Controllers
{
    [Route("api/subcategories")]
    [ApiController]
    public class SubCategoryController : BaseController
    {
        private readonly ISubCategoryServices _subCategoryServices;
        private readonly ILogger<SubCategoryController> _logger;

        public SubCategoryController(
            ISubCategoryLinkBuilder linkBuilder,
            ISubCategoryServices subCategoryServices,
            ILogger<SubCategoryController> logger): base(linkBuilder) 
        {
           
            _subCategoryServices = subCategoryServices;
            _logger = logger;
        }

       

      

        // User: Get active, non-deleted subcategory by ID
        [HttpGet("{id}")]
        [AllowAnonymous]
        [ActionName(nameof(GetById))]
        public async Task<ActionResult<ApiResponse<SubCategoryDtoWithData>>> GetById(int id, [FromQuery] bool? isActive = null, [FromQuery] bool? isDeleted = null)
        {
            _logger.LogInformation($"Executing {nameof(GetById)} for id: {id}");
            if (id <= 0)
                return BadRequest(ApiResponse<SubCategoryDtoWithData>.CreateErrorResponse("Invalid subcategory ID", new ErrorResponse("Validation", new List<string> { "ID must be greater than 0" }), 400));

            bool? effectiveIsActive = isActive;
            bool? effectiveIsDeleted = isDeleted;

            var isAdmin =  HasManagementRole();
            if (!isAdmin)
            {
                effectiveIsActive = true;
                effectiveIsDeleted = false;
            }

            var result = await _subCategoryServices.GetSubCategoryByIdAsync(id, effectiveIsActive, effectiveIsDeleted, isAdmin);
            return HandleResult(result, nameof(GetById), id);
        }

        [HttpPost]
        [Authorize(Roles = "Admin")]
        [ActionName(nameof(Create))]
        public async Task<ActionResult<ApiResponse<SubCategoryDto>>> Create([FromBody] CreateSubCategoryDto subCategoryDto)
        {
            _logger.LogInformation($"Executing {nameof(Create)}");
            if (!ModelState.IsValid)
            {
                var errors = GetModelErrors();
                return BadRequest(ApiResponse<SubCategoryDto>.CreateErrorResponse("Check On Data", new ErrorResponse("Invalid Data", errors)));
            }
            var userId = GetUserId();
            var result = await _subCategoryServices.CreateAsync(subCategoryDto, userId);
            return HandleResult(result, nameof(Create));
        }

        [HttpDelete("{id}")]
        [Authorize(Roles = "Admin,SuperAdmin")]
        [ActionName(nameof(Delete))]
        public async Task<ActionResult<ApiResponse<bool>>> Delete(int id)
        {
            _logger.LogInformation($"Executing {nameof(Delete)} for id: {id}");
            var userId = GetUserId();
            var result = await _subCategoryServices.DeleteAsync(id, userId);
            return HandleResult(result, nameof(Delete));
        }

        [HttpPatch("{id}/restore")]
        [Authorize(Roles = "Admin,SuperAdmin")]
        [ActionName(nameof(Restore))]
        public async Task<ActionResult<ApiResponse<SubCategoryDto>>> Restore(int id)
        {
            _logger.LogInformation($"Executing {nameof(Restore)} for id: {id}");
            var userId = GetUserId();
            var result = await _subCategoryServices.ReturnRemovedSubCategoryAsync(id, userId);
            return HandleResult(result, nameof(Restore), id);
        }

        [HttpPut("{id}")]
        [Authorize(Roles = "Admin,SuperAdmin")]
        [ActionName(nameof(Update))]
        public async Task<ActionResult<ApiResponse<SubCategoryDto>>> Update(int id, [FromBody] UpdateSubCategoryDto subCategoryDto)
        {
            _logger.LogInformation($"Executing {nameof(Update)} for id: {id}");
            if (!ModelState.IsValid)
            {
                var errors = GetModelErrors();
                return BadRequest(ApiResponse<SubCategoryDto>.CreateErrorResponse("Check on data", new ErrorResponse("Invalid Data", errors)));
            }
            var userId = GetUserId();
            var result = await _subCategoryServices.UpdateAsync(id, subCategoryDto, userId);
            return HandleResult(result, nameof(Update), id);
        }

        [HttpPost("{id}/images/main")]
        [Authorize(Roles = "Admin,SuperAdmin")]
        [ActionName(nameof(AddMainImage))]
        public async Task<ActionResult<ApiResponse<ImageDto>>> AddMainImage(int id, [FromForm] AddMainImageDto mainImage)
        {
            _logger.LogInformation($"Executing {nameof(AddMainImage)} for id: {id}");
            if (!ModelState.IsValid)
            {
                var errors = GetModelErrors();
                return BadRequest(ApiResponse<SubCategoryDto>.CreateErrorResponse("Check on data", new ErrorResponse("Invalid Data", errors)));
            }
            if (mainImage.Image == null || mainImage.Image.Length == 0)
            {
                return BadRequest(ApiResponse<ImageDto>.CreateErrorResponse("Image Can't Empty", new ErrorResponse("Validation", new List<string> { "Main image is required." }), 400));
            }
            var userId = GetUserId();
            var result = await _subCategoryServices.AddMainImageToSubCategoryAsync(id, mainImage.Image, userId);
            return HandleResult(result, nameof(AddMainImage), id);
        }

        [HttpPost("{id}/images")]
        [Authorize(Roles = "Admin,SuperAdmin")]
        [ActionName(nameof(AddImages))]
        public async Task<ActionResult<ApiResponse<List<ImageDto>>>> AddImages(int id, [FromForm] AddImagesDto images)
        {
            _logger.LogInformation($"Executing {nameof(AddImages)} for id: {id}");
            if (!ModelState.IsValid)
            {
                var errors = GetModelErrors();
                return BadRequest(ApiResponse<SubCategoryDto>.CreateErrorResponse("Check on data", new ErrorResponse("Invalid Data", errors)));
            }
            if (images.Images == null || !images.Images.Any())
            {
                return BadRequest(ApiResponse<List<ImageDto>>.CreateErrorResponse("Image Can't Empty", new ErrorResponse("Validation", new List<string> { "At least one image is required." }), 400));
            }
            var userId = GetUserId();
            var result = await _subCategoryServices.AddImagesToSubCategoryAsync(id, images.Images, userId);
            return HandleResult(result, nameof(AddImages), id);
        }

        [HttpDelete("{id}/images/{imageId}")]
        [Authorize(Roles = "Admin,SuperAdmin")]
        [ActionName(nameof(RemoveImage))]
        public async Task<ActionResult<ApiResponse<bool>>> RemoveImage(int id, int imageId)
        {
            _logger.LogInformation($"Executing {nameof(RemoveImage)} for id: {id}, imageId: {imageId}");
            var userId = GetUserId();
            var result = await _subCategoryServices.RemoveImageFromSubCategoryAsync(id, imageId, userId);
            return HandleResult(result, nameof(RemoveImage), id);
        }

     

        [HttpPatch("{id}/activate")]
        [Authorize(Roles = "Admin,SuperAdmin")]
        [ActionName(nameof(Activate))]
        public async Task<ActionResult<ApiResponse<bool>>> Activate(int id)
        {
            _logger.LogInformation($"Executing {nameof(Activate)} for id: {id}");
            var userId = GetUserId();
            var result = await _subCategoryServices.ActivateSubCategoryAsync(id, userId);
            return HandleResult(result, nameof(Activate));
        }

        [HttpPatch("{id}/deactivate")]
        [Authorize(Roles = "Admin,SuperAdmin")]
        [ActionName(nameof(Deactivate))]
        public async Task<ActionResult<ApiResponse<bool>>> Deactivate(int id)
        {
            _logger.LogInformation($"Executing {nameof(Deactivate)} for id: {id}");
            var userId = GetUserId();
            var result = await _subCategoryServices.DeactivateSubCategoryAsync(id, userId);
            return HandleResult(result, nameof(Deactivate));
        }

        [HttpGet()]
        [AllowAnonymous]
        public async Task<ActionResult<ApiResponse<List<SubCategoryDto>>>> Search(
            [FromQuery] string key,
            [FromQuery] bool? isActive = null,
            [FromQuery] bool? includeDeleted = null,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 10)
        {

            if (page <= 0 || pageSize <= 0 || pageSize > 100)
            {
                return BadRequest(ApiResponse<string>.CreateErrorResponse(
                    "Invalid pagination",
                    new ErrorResponse("Validation", new List<string> { "page and pageSize must be greater than 0, pageSize <= 100" }),
                    StatusCodes.Status400BadRequest));
            }
            bool? effectiveIsActive = isActive;
            bool? effectiveIncludeDeleted = includeDeleted ;
            var isAdmin =  HasManagementRole();
            if (!isAdmin)
            {
                effectiveIsActive = true;
                effectiveIncludeDeleted = false;
            }

            _logger.LogInformation($"Executing {nameof(Search)} with key={key}, isActive={effectiveIsActive}, includeDeleted={effectiveIncludeDeleted}, page={page}, pageSize={pageSize}");

            var result = await _subCategoryServices.FilterAsync(key, effectiveIsActive, effectiveIncludeDeleted, page, pageSize, isAdmin);
            return HandleResult(result, nameof(Search));
        }




    }
}
		

	
