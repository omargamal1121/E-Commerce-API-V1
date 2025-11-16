using ApplicationLayer.DtoModels.CollectionDtos;
using DomainLayer.Models;


namespace ApplicationLayer.Services.CollectionServices
{
    public interface ICollectionMapper
    {
        CollectionDto ToCollectionDto(Collection c, bool IsAdmin = false);
        public IQueryable<CollectionDto> CollectionSelectorWithData(IQueryable<Collection> collections, bool IsAdmin = false);
        public IQueryable<CollectionSummaryDto> CollectionSelector(IQueryable<Collection> queryable);
        CollectionSummaryDto ToCollectionSummaryDto(Collection c);
    }
}


