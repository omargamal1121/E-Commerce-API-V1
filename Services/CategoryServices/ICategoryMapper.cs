using E_Commerce.DtoModels.CategoryDtos;
using E_Commerce.DtoModels.ImagesDtos;
using E_Commerce.DtoModels.SubCategorydto;
using E_Commerce.Models;
using System.Linq.Expressions;

namespace E_Commerce.Services.CategoryServices
{
	public interface ICategoryMapper
	{
		CategoryDto ToCategoryDto(Category c);
		public IQueryable<CategorywithdataDto> CategorySelectorWithData(IQueryable<Category> categories);
		public IQueryable<CategoryDto> CategorySelector(IQueryable<Category> queryable);
		CategorywithdataDto ToCategoryWithDataDto(Category c);


	}
}
