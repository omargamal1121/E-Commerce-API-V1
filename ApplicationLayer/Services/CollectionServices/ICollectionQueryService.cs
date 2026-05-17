using Application.DtoModels.CollectionDtos;
using Application.DtoModels.Responses;

namespace Application.Services.CollectionServices
{
    public interface ICollectionQueryService
    {
        Task<Result<CollectionDto>> GetCollectionByIdAsync(int collectionId, bool? IsActive = null, bool? IsDeleted = null, bool IsAdmin = false);
        Task<Result<List<CollectionSummaryDto>>> SearchCollectionsAsync(string? searchTerm, bool? IsActive = null, bool? IsDeleted = null, int page = 1, int pagesize = 10, bool IsAdmin = false);
        Task<Result<List<CollectionSummaryDto>>> GetCollectionsByProductIdAsync(int productId, bool? IsActive = null, bool? IsDeleted = null, bool IsAdmin = false);
    }
}


