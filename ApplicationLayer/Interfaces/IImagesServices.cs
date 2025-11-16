using DomainLayer.Models;
using ApplicationLayer.Services;
using Microsoft.AspNetCore.Http;

namespace ApplicationLayer.Interfaces
{
	public interface IImagesServices
	{
		bool IsValidExtension(string extension);
		public Task<Result<List<string>>> DeleteImagesAsync(List<Image> images);
		string GetFolderPath(params string[] folders);
		Task<Result<Image>> SaveCustomerImageAsync(IFormFile image, string userId);
		
		// Single image methods
		Task<Result<Image>> SaveCategoryImageAsync(IFormFile image, int id, string userId);
		Task<Result<Image>> SaveCollectionImageAsync(IFormFile image, int id, string userId);
		Task<Result<Image>> SaveProductImageAsync(IFormFile image, int id, string userId);
		Task<Result<Image>> SaveSubCategoryImageAsync(IFormFile image, int id, string userId);
		
		// Multiple images methods
		Task<Result<List<Image>>> SaveCategoryImagesAsync(List<IFormFile> images, int id, string userId);
		Task<Result<List<Image>>> SaveCollectionImagesAsync(List<IFormFile> images, int id, string userId);
		Task<Result<List<Image>>> SaveProductImagesAsync(List<IFormFile> images, int id, string userId);
		Task<Result<List<Image>>> SaveSubCategoryImagesAsync(List<IFormFile> images, int id, string userId);
		
		// Main image methods
		Task<Result<Image>> SaveMainCategoryImageAsync(IFormFile image, int id, string userId);
		Task<Result<Image>> SaveMainCollectionImageAsync(IFormFile image, int id, string userId);
		Task<Result<Image>> SaveMainProductImageAsync(IFormFile image, int id, string userId);
		Task<Result<Image>> SaveMainSubCategoryImageAsync(IFormFile image, int id, string userId);
		
		Task<Result<string>> DeleteImageAsync(Image image);
		Task<Result<string>> DeleteImageAsync(int imageid);

    }
}


