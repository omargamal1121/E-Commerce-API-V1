using ApplicationLayer.DtoModels.CollectionDtos;
using ApplicationLayer.DtoModels.ImagesDtos;
using ApplicationLayer.DtoModels.Responses;
using Microsoft.AspNetCore.Http;

namespace ApplicationLayer.Services.CollectionServices
{
    public interface ICollectionImageService
    {
        Task<Result<List<ImageDto>>> AddImagesToCollectionAsync(int collectionid, List<IFormFile> images, string userId);
        Task<Result<ImageDto>> AddMainImageToCollectionAsync(int collectionid, IFormFile mainImage, string userId);
        Task<Result<bool>> RemoveImageFromCollectionAsync(int categoryId, int imageId, string userId);
    }
}


