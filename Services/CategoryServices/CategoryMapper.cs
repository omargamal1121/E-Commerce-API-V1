using E_Commerce.DtoModels.CategoryDtos;
using E_Commerce.DtoModels.ImagesDtos;
using E_Commerce.DtoModels.SubCategorydto;
using E_Commerce.Models;
using System.Linq.Expressions;

namespace E_Commerce.Services.CategoryServices
{
	public class CategoryMapper: ICategoryMapper
	{

		public  IQueryable<CategorywithdataDto>  CategorySelectorWithData (IQueryable<Category> categories)
		{

			return categories.Select(c => new CategorywithdataDto
			{
				Id = c.Id,
				Name = c.Name,
				Description = c.Description,
				IsActive = c.IsActive,
				CreatedAt = c.CreatedAt,
				DeletedAt = c.DeletedAt,
				ModifiedAt = c.ModifiedAt,
				DisplayOrder = c.DisplayOrder,
				SubCategories = c.SubCategories.Where(sc => sc.IsActive && sc.DeletedAt == null).Select(sc => new SubCategoryDto
				{
					Id = sc.Id,
					Name = sc.Name,
					Description = sc.Description,
					IsActive = sc.IsActive,
					CreatedAt = sc.CreatedAt,
					ModifiedAt = sc.ModifiedAt,
					DeletedAt = sc.DeletedAt,
					Images = sc.Images.Select(i => new ImageDto
					{
						Id = i.Id,
						Url = i.Url,
						IsMain = i.IsMain
					}).ToList()
				}).ToList(),
				Images = c.Images.Select(i => new ImageDto
				{
					Id = i.Id,
					Url = i.Url,
					IsMain = i.IsMain
				}).ToList()
			});

		} 
		

		public IQueryable< CategoryDto> CategorySelector (IQueryable<Category> queryable)  {  return queryable.Select(c=>new CategoryDto
		{
			Id = c.Id,
			Name = c.Name,
			Description = c.Description,
			IsActive = c.IsActive,
			CreatedAt = c.CreatedAt,
			DeletedAt = c.DeletedAt,
			ModifiedAt = c.ModifiedAt,
			DisplayOrder = c.DisplayOrder,
			Images = c.Images.Select(i => new ImageDto
			{
				Id = i.Id,
				Url = i.Url,
				IsMain = i.IsMain
			}).ToList()
		}
		); }
		public CategoryDto ToCategoryDto(Category c) => new CategoryDto
		{
			Id = c.Id,
			Name = c.Name,
			Description = c.Description,
			IsActive = c.IsActive,
			CreatedAt = c.CreatedAt,
			DeletedAt = c.DeletedAt,
			ModifiedAt = c.ModifiedAt,
			DisplayOrder = c.DisplayOrder,
			Images = c.Images?.Select(i => new ImageDto
			{
				Id = i.Id,
				Url = i.Url,
				IsMain = i.IsMain
			}).ToList() ?? new List<ImageDto>()
		};

		public CategorywithdataDto ToCategoryWithDataDto(Category c) => new CategorywithdataDto
		{
			Id = c.Id,
			Name = c.Name,
			Description = c.Description,
			DisplayOrder = c.DisplayOrder,
			IsActive = c.IsActive,
			CreatedAt = c.CreatedAt,
			ModifiedAt = c.ModifiedAt,
			DeletedAt = c.DeletedAt,
			Images = c.Images?.Select(i => new ImageDto
			{
				Id = i.Id,
				Url = i.Url,
				IsMain = i.IsMain
			}).ToList() ?? new List<ImageDto>(),
			SubCategories = c.SubCategories?.Select(sc => new SubCategoryDto
			{
				Id = sc.Id,
				Name = sc.Name,
				Description = sc.Description,
				IsActive = sc.IsActive,
				CreatedAt = sc.CreatedAt,
				ModifiedAt = sc.ModifiedAt,
				DeletedAt = sc.DeletedAt,
				Images = sc.Images?.Select(i => new ImageDto
				{
					Id = i.Id,
					Url = i.Url,
					IsMain = i.IsMain
				}).ToList() ?? new List<ImageDto>()
			}).ToList() ?? new List<SubCategoryDto>()
		};



	}
}
