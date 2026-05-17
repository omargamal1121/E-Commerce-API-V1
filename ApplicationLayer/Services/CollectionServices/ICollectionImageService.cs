using Application.DtoModels.CollectionDtos;
using Application.DtoModels.ImagesDtos;
using Application.DtoModels.Responses;
using Microsoft.AspNetCore.Http;

namespace Application.Services.CollectionServices
{
    public interface ICollectionImageService
    {
        Task<Result<List<ImageDto>>> AddImagesToCollectionAsync(int collectionid, List<IFormFile> images, string userId);
        Task<Result<ImageDto>> AddMainImageToCollectionAsync(int collectionid, IFormFile mainImage, string userId);
        Task<Result<bool>> RemoveImageFromCollectionAsync(int categoryId, int imageId, string userId);
    }
}


