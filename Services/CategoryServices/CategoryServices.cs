using AutoMapper;
using E_Commerce.DtoModels;
using E_Commerce.DtoModels.CategoryDtos;
using E_Commerce.DtoModels.ImagesDtos;
using E_Commerce.DtoModels.ProductDtos;
using E_Commerce.DtoModels.Responses;
using E_Commerce.DtoModels.SubCategorydto;
using E_Commerce.Enums;
using E_Commerce.ErrorHnadling;
using E_Commerce.Interfaces;
using E_Commerce.Models;
using E_Commerce.Services.AdminOpreationServices;
using E_Commerce.Services.Cache;
using E_Commerce.Services.CategoryServices;
using E_Commerce.Services.EmailServices;
using E_Commerce.UOW;
using Hangfire;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.Linq.Expressions;
using System.Threading.Tasks;

namespace E_Commerce.Services.CategoryServices
{
	public class CategoryServices : ICategoryServices
	{
		private readonly ILogger<CategoryServices> _logger;
		private readonly ICategoryCommandService _categoryCommandService;
		private readonly ICategoryQueryService _categoryQueryService;
		private readonly ICategoryImageService _categoryImageServices;

		public CategoryServices(

			ICategoryCommandService categoryCommandService,
			ICategoryQueryService categoryQueryService,
			ICategoryImageService categoryImageServices,


			ILogger<CategoryServices> logger
		)
		{
			_categoryCommandService = categoryCommandService;
			_categoryImageServices = categoryImageServices;
			_categoryQueryService = categoryQueryService;
			_logger = logger;
		}

		public Task<Result<bool>> IsExsistAsync(int id)
		{
			throw new NotImplementedException();
		}

		public Task<Result<CategoryDto>> CreateAsync(CreateCategotyDto categoty, string userid)
		{
			return _categoryCommandService.CreateAsync(categoty, userid);
		}

		public Task<Result<CategorywithdataDto>> GetCategoryByIdAsync(int id, bool? isActive = null, bool? includeDeleted = null)
		{
			return _categoryQueryService.GetCategoryByIdAsync(id, isActive, includeDeleted);
		}

		public Task<Result<bool>> DeleteAsync(int id, string userid)
		{
			return _categoryCommandService.DeleteAsync(id, userid);
		}

		public Task<Result<CategoryDto>> UpdateAsync(int categoryid, UpdateCategoryDto category, string userid)
		{
			return _categoryCommandService.UpdateAsync(categoryid, category, userid);
		}

		public Task<Result<List<CategoryDto>>> FilterAsync(string? search, bool? isActive, bool? includeDeleted, int page, int pageSize)
		{
			return _categoryQueryService.FilterAsync(search, isActive, includeDeleted, page, pageSize);
		}

		public Task<Result<CategoryDto>> RestoreAsync(int id, string userid)
		{
			return _categoryCommandService.RestoreAsync(id, userid);
		}

		public Task<Result<List<ImageDto>>> AddImagesToCategoryAsync(int categoryId, List<IFormFile> images, string userId)
		{
			return _categoryImageServices.AddImagesToCategoryAsync(categoryId, images, userId);
		}

		public Task<Result<ImageDto>> AddMainImageToCategoryAsync(int categoryId, IFormFile mainImage, string userId)
		{
			return _categoryImageServices.AddMainImageToCategoryAsync(categoryId, mainImage, userId);
		}

		public Task<Result<bool>> RemoveImageFromCategoryAsync(int categoryId, int imageId, string userId)
		{
			return _categoryImageServices.RemoveImageFromCategoryAsync(categoryId, imageId, userId);
		}

		public Task<Result<bool>> ActivateCategoryAsync(int categoryId, string userId)
		{
			return _categoryCommandService.ActivateAsync(categoryId, userId);
		}

		public Task<Result<bool>> DeactivateCategoryAsync(int categoryId, string userId)
		{
			return _categoryCommandService.DeactivateAsync(categoryId, userId);
		}
	}
}
