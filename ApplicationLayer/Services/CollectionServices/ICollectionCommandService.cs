using Application.DtoModels.CollectionDtos;
using Application.DtoModels.Responses;

namespace Application.Services.CollectionServices
{
    public interface ICollectionCommandService
    {
        Task<Result<CollectionSummaryDto>> CreateCollectionAsync(CreateCollectionDto collectionDto, string userid);
        Task<Result<CollectionSummaryDto>> UpdateCollectionAsync(int collectionId, UpdateCollectionDto collectionDto, string userid);
        Task<Result<bool>> DeleteCollectionAsync(int collectionId, string userid);
        Task<Result<bool>> ActivateCollectionAsync(int collectionId, string userId);
        Task<Result<bool>> DeactivateCollectionAsync(int collectionId, string userId);
        Task<Result<bool>> UpdateCollectionDisplayOrderAsync(int collectionId, int displayOrder, string userid);
        Task<Result<bool>> AddProductsToCollectionAsync(int collectionId, AddProductsToCollectionDto productsDto, string userId);
        Task<Result<bool>> RemoveProductsFromCollectionAsync(int collectionId, RemoveProductsFromCollectionDto productsDto, string userId);
        Task DeactivateCollectionsWithoutActiveProductsAsync(int productId);
        public  Task<Result<bool>> RestoreCollectionAsync(int collectionId, string userid);

	}
}


