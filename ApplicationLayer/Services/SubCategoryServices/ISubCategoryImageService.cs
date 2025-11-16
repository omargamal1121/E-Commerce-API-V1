using ApplicationLayer.DtoModels.ImagesDtos;
using DomainLayer.Models;
using ApplicationLayer.Services;
using Microsoft.AspNetCore.Http;

namespace ApplicationLayer.Services.SubCategoryServices
{
    public interface ISubCategoryImageService
    {
		Task<Result<List<ImageDto>>> AddImagesToSubCategoryAsync(int subCategoryId, List<IFormFile> images, string userId);
		Task<Result<ImageDto>> AddMainImageToSubCategoryAsync(int subCategoryId, IFormFile mainImage, string userId);
		Task<Result<bool>> RemoveImageFromSubCategoryAsync(int subCategoryId, int imageId, string userId);

	}
}

