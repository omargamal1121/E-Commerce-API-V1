using E_Commerce.DtoModels.ImagesDtos;
using E_Commerce.Models;
using E_Commerce.Services;

namespace E_Commerce.Services.SubCategoryServices
{
    public interface ISubCategoryImageService
    {
		Task<Result<List<ImageDto>>> AddImagesToSubCategoryAsync(int subCategoryId, List<IFormFile> images, string userId);
		Task<Result<ImageDto>> AddMainImageToSubCategoryAsync(int subCategoryId, IFormFile mainImage, string userId);
		Task<Result<bool>> RemoveImageFromSubCategoryAsync(int subCategoryId, int imageId, string userId);

	}
}