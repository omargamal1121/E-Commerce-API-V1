using Application.DtoModels.CollectionDtos;
using Domain.Models;


namespace Application.Services.CollectionServices
{
    public interface ICollectionMapper
    {
        CollectionDto ToCollectionDto(Collection c, bool IsAdmin = false);
        public IQueryable<CollectionDto> CollectionSelectorWithData(IQueryable<Collection> collections, bool IsAdmin = false);
        public IQueryable<CollectionSummaryDto> CollectionSelector(IQueryable<Collection> queryable, bool IsAdmin = false);
        CollectionSummaryDto ToCollectionSummaryDto(Collection c);
    }
}


