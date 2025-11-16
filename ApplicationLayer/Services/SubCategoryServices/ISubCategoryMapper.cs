using ApplicationLayer.DtoModels.SubCategorydto;
using DomainLayer.Models;
using System.Linq.Expressions;

namespace ApplicationLayer.Services.SubCategoryServices
{
    public interface ISubCategoryMapper
    {
        SubCategoryDto ToSubCategoryDto(SubCategory s);

        public SubCategoryDtoWithData MapToSubCategoryDtoWithData(SubCategory subCategory, bool IsAdmin = false);

		public IQueryable<SubCategoryDto> SubCategorySelector(IQueryable<SubCategory> queryable);
        public IQueryable<SubCategoryDtoWithData> SubCategorySelectorWithData(IQueryable<SubCategory> subCategories, bool IsAdmin = false);
    }
}

