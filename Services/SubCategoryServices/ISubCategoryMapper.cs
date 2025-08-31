using E_Commerce.DtoModels.SubCategorydto;
using E_Commerce.Models;
using System.Linq.Expressions;

namespace E_Commerce.Services.SubCategoryServices
{
    public interface ISubCategoryMapper
    {
        SubCategoryDto ToSubCategoryDto(SubCategory s);

        public SubCategoryDtoWithData MapToSubCategoryDtoWithData(SubCategory subCategory);

		public IQueryable<SubCategoryDto> SubCategorySelector(IQueryable<SubCategory> queryable);
        public IQueryable<SubCategoryDtoWithData> SubCategorySelectorWithData(IQueryable<SubCategory> subCategories);
    }
}