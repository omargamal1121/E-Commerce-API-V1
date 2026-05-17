using Application.DtoModels.CategoryDtos;
using Application.DtoModels.ImagesDtos;
using Application.DtoModels.SubCategorydto;
using Domain.Models;
using System.Linq.Expressions;

namespace Application.Services.CategoryServices
{
	public interface ICategoryMapper
	{
		CategoryDto ToCategoryDto(Category c);
		public IQueryable<CategorywithdataDto> CategorySelectorWithData(IQueryable<Category> categories, bool IsAdmin = false);
		public IQueryable<CategoryDto> CategorySelector(IQueryable<Category> queryable);
		CategorywithdataDto ToCategoryWithDataDto(Category c,bool IsAdmin=false);


	}
}


