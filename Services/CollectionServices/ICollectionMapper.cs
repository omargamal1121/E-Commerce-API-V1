using E_Commerce.DtoModels.CollectionDtos;
using E_Commerce.DtoModels.ImagesDtos;
using E_Commerce.DtoModels.ProductDtos;
using E_Commerce.Models;
using System.Linq.Expressions;

namespace E_Commerce.Services.Collection
{
    public interface ICollectionMapper
    {
        CollectionDto ToCollectionDto(Models.Collection c, bool IsAdmin = false);
        public IQueryable<CollectionDto> CollectionSelectorWithData(IQueryable<Models.Collection> collections, bool IsAdmin = false);
        public IQueryable<CollectionSummaryDto> CollectionSelector(IQueryable<Models.Collection> queryable);
        CollectionSummaryDto ToCollectionSummaryDto(Models.Collection c);
    }
}
