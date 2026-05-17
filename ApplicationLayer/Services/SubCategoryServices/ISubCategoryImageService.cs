using Application.DtoModels.ImagesDtos;
using Domain.Models;
using Application.Services;
using Microsoft.AspNetCore.Http;

namespace Application.Services.SubCategoryServices
{
    public interface ISubCategoryImageService
    {
		Task<Result<List<ImageDto>>> AddImagesToSubCategoryAsync(int subCategoryId, List<IFormFile> images, string userId);
		Task<Result<ImageDto>> AddMainImageToSubCategoryAsync(int subCategoryId, IFormFile mainImage, string userId);
		Task<Result<bool>> RemoveImageFromSubCategoryAsync(int subCategoryId, int imageId, string userId);

	}
}

