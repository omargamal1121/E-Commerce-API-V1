
using ApplicationLayer.DtoModels.ImagesDtos;
using ApplicationLayer.DtoModels.ProductDtos;
using ApplicationLayer.DtoModels.SubCategorydto;
using DomainLayer.Enums;
using ApplicationLayer.Interfaces;

using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;


namespace ApplicationLayer.Services.SubCategoryServices
{
    public class SubCategoryServices : ISubCategoryServices
    {
        private readonly ILogger<SubCategoryServices> _logger;

        private readonly ISubCategoryCommandService _subCategoryCommandService;
        private readonly ISubCategoryImageService _subCategoryImageService;
        private readonly ISubCategoryQueryService _subCategoryQueryService;


        public SubCategoryServices(

            ILogger<SubCategoryServices> logger,
            ISubCategoryCommandService subCategoryCommandService,
            ISubCategoryImageService subCategoryImageService,
            ISubCategoryQueryService subCategoryQueryService
            )
        {
            _logger = logger;
            _subCategoryCommandService = subCategoryCommandService;
            _subCategoryImageService = subCategoryImageService;
            _subCategoryQueryService = subCategoryQueryService;
		}


     

   
      


     


		public async Task<Result<SubCategoryDtoWithData>> GetSubCategoryByIdAsync(int id, bool? isActive = null, bool? isDeleted = null, bool IsAdmin = false)
		{
			return await _subCategoryQueryService.GetSubCategoryByIdAsync(id, isActive, isDeleted, IsAdmin);
		}


		public async Task<Result<SubCategoryDto>> CreateAsync(CreateSubCategoryDto subCategory, string userid)
        {
            return await _subCategoryCommandService.CreateAsync(subCategory, userid);
		}

		public async Task<Result<List<ImageDto>>> AddImagesToSubCategoryAsync(int subCategoryId, List<IFormFile> images, string userId)
		{return await _subCategoryImageService.AddImagesToSubCategoryAsync(subCategoryId, images, userId);
		}

		public async Task<Result<bool>> DeleteAsync(int id, string userid)
        {
            return await _subCategoryCommandService.DeleteAsync(id, userid);
		}

        public async Task<Result<List<SubCategoryDto>>> FilterAsync(string? search,bool? isActive,bool?isDeleted,int page,int pageSize, bool IsAdmin = false)
        {
            return await _subCategoryQueryService.FilterAsync(search, isActive, isDeleted, page, pageSize, IsAdmin);
		}

	
	



		public async Task<Result<SubCategoryDto>> ReturnRemovedSubCategoryAsync(int id, string userid)
        {
          return await _subCategoryCommandService.ReturnRemovedSubCategoryAsync(id, userid);
		}

        public async Task<Result<SubCategoryDto>> UpdateAsync(int subCategoryId, UpdateSubCategoryDto subCategory, string userid)
        {
            return await _subCategoryCommandService.UpdateAsync(subCategoryId, subCategory, userid);
		}

		public async Task<Result<bool>> RemoveImageFromSubCategoryAsync(int subCategoryId, int imageId, string userId)
		{
		return await _subCategoryImageService.RemoveImageFromSubCategoryAsync(subCategoryId, imageId, userId);
		}



        public async Task<Result<bool>> ActivateSubCategoryAsync(int subCategoryId, string userId)
        {
            return await _subCategoryCommandService.ActivateSubCategoryAsync(subCategoryId, userId);
		}

		public async Task<Result<bool>> DeactivateSubCategoryAsync(int subCategoryId, string userId)
		{
			return await _subCategoryCommandService.DeactivateSubCategoryAsync(subCategoryId, userId);
				}

	
		public Task<Result<ImageDto>> AddMainImageToSubCategoryAsync(int subCategoryId, IFormFile mainImage, string userId)
		{
			return _subCategoryImageService.AddMainImageToSubCategoryAsync(subCategoryId, mainImage, userId);
		}

		public Task DeactivateSubCategoryIfAllProductsAreInactiveAsync(int subCategoryId, string userId)
		{
			return _subCategoryCommandService.DeactivateSubCategoryIfAllProductsAreInactiveAsync(subCategoryId, userId);
		}
	}
} 

