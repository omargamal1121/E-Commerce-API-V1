using ApplicationLayer.DtoModels.CategoryDtos;
using ApplicationLayer.DtoModels.ImagesDtos;
using ApplicationLayer.DtoModels.SubCategorydto;
using DomainLayer.Models;
using System.Linq.Expressions;

namespace ApplicationLayer.Services.CategoryServices
{
	public interface ICategoryMapper
	{
		CategoryDto ToCategoryDto(Category c);
		public IQueryable<CategorywithdataDto> CategorySelectorWithData(IQueryable<Category> categories, bool IsAdmin = false);
		public IQueryable<CategoryDto> CategorySelector(IQueryable<Category> queryable);
		CategorywithdataDto ToCategoryWithDataDto(Category c,bool IsAdmin=false);


	}
}


