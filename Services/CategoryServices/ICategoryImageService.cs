using E_Commerce.DtoModels.ImagesDtos;

namespace E_Commerce.Services.CategoryServices
{
	public interface ICategoryImageService
	{

		Task<Result<List<ImageDto>>> AddImagesToCategoryAsync(int categoryId, List<IFormFile> images, string userId);
		Task<Result<ImageDto>> AddMainImageToCategoryAsync(int categoryId, IFormFile mainImage, string userId);
		Task<Result<bool>> RemoveImageFromCategoryAsync(int categoryId, int imageId, string userId);
	}
}
