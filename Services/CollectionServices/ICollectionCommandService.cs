using E_Commerce.DtoModels.CollectionDtos;
using E_Commerce.DtoModels.Responses;

namespace E_Commerce.Services.Collection
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

	}
}
