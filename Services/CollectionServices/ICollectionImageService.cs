using E_Commerce.DtoModels.CollectionDtos;
using E_Commerce.DtoModels.ImagesDtos;
using E_Commerce.DtoModels.Responses;

namespace E_Commerce.Services.Collection
{
    public interface ICollectionImageService
    {
        Task<Result<List<ImageDto>>> AddImagesToCollectionAsync(int collectionid, List<IFormFile> images, string userId);
        Task<Result<ImageDto>> AddMainImageToCollectionAsync(int collectionid, IFormFile mainImage, string userId);
        Task<Result<bool>> RemoveImageFromCollectionAsync(int categoryId, int imageId, string userId);
    }
}
