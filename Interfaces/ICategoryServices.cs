using E_Commerce.DtoModels.CategoryDtos;
using E_Commerce.DtoModels.ImagesDtos;
using E_Commerce.Services;
using Microsoft.AspNetCore.Http;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace E_Commerce.Interfaces
{
	public interface ICategoryServices
	{

		Task<Result<bool>> IsExsistAsync(int id);
		
		Task<Result<CategoryDto>> CreateAsync(CreateCategotyDto categoty, string userid);
		Task<Result<CategorywithdataDto>> GetCategoryByIdAsync(int id, bool? isActive = null, bool? includeDeleted = null);
		Task<Result<bool>> DeleteAsync(int id, string userid);
		Task<Result<CategoryDto>> UpdateAsync(int categoryid, UpdateCategoryDto category, string userid);
		public  Task<Result<List<CategoryDto>>> FilterAsync(string? search,bool? isActive,bool?includeDeleted,int page,int pageSize);
		Task<Result<CategoryDto>> ReturnRemovedCategoryAsync(int id, string userid);
		Task<Result<List<ImageDto>>> AddImagesToCategoryAsync(int categoryId, List<IFormFile> images, string userId);
		Task<Result<ImageDto>> AddMainImageToCategoryAsync(int categoryId, IFormFile mainImage, string userId);
		Task<Result<bool>> RemoveImageFromCategoryAsync(int categoryId, int imageId, string userId);
		Task<Result<bool>> ActivateCategoryAsync(int categoryId, string userId);
		Task<Result<bool>> DeactivateCategoryAsync(int categoryId, string userId);
	}
}
