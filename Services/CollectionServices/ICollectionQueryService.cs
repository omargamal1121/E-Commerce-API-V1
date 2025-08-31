using E_Commerce.DtoModels.CollectionDtos;
using E_Commerce.DtoModels.Responses;

namespace E_Commerce.Services.Collection
{
    public interface ICollectionQueryService
    {
        Task<Result<CollectionDto>> GetCollectionByIdAsync(int collectionId, bool? IsActive = null, bool? IsDeleted = null);
        Task<Result<List<CollectionSummaryDto>>> SearchCollectionsAsync(string? searchTerm, bool? IsActive = null, bool? IsDeleted = null, int page = 1, int pagesize = 10);
    }
}
