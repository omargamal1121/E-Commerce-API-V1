using ApplicationLayer.DtoModels.ImagesDtos;
using ApplicationLayer.DtoModels.ProductDtos;
using ApplicationLayer.DtoModels.SubCategorydto;
using DomainLayer.Models;
using ApplicationLayer.Services.SubCategoryServices;

namespace ApplicationLayer.Services.SubCategoryServices
{
    public class SubCategoryMapper : ISubCategoryMapper
    {
       
		public SubCategoryDto ToSubCategoryDto(SubCategory subCategory)
		{
			return new SubCategoryDto
			{
				Id = subCategory.Id,
				Name = subCategory.Name,
				IsActive = subCategory.IsActive,
				CreatedAt = subCategory.CreatedAt,
				ModifiedAt = subCategory.ModifiedAt,
				DeletedAt = subCategory.DeletedAt,
				Description = subCategory.Description,
				Images = subCategory.Images?.Where(i=>i.DeletedAt==null).Select(img => new ImageDto
				{
					Id = img.Id,
					Url = img.Url
				}).ToList()
			};
		}

		IQueryable<SubCategoryDto> ISubCategoryMapper.SubCategorySelector(IQueryable<SubCategory> queryable)
		{
			return queryable.Select(subCategory=>
			new SubCategoryDto
			{
				Id = subCategory.Id,
				Name = subCategory.Name,
				IsActive = subCategory.IsActive,
				CreatedAt = subCategory.CreatedAt,
				ModifiedAt = subCategory.ModifiedAt,
				DeletedAt = subCategory.DeletedAt,
				Description = subCategory.Description,
				Images = subCategory.Images
		.Where(img => img.DeletedAt == null)
		.Select(img => new ImageDto
		{
			Id = img.Id,
			Url = img.Url
		}).ToList(),

			});
		}

		public	IQueryable<SubCategoryDtoWithData> SubCategorySelectorWithData(IQueryable<SubCategory> subCategories,bool IsAdmin=false)
		{
			return subCategories.Select(subCategory =>
			new SubCategoryDtoWithData
			{
				Id = subCategory.Id,
				Name = subCategory.Name,
				IsActive = subCategory.IsActive,
				CreatedAt = subCategory.CreatedAt,
				ModifiedAt = subCategory.ModifiedAt,
				DeletedAt = subCategory.DeletedAt,
				Description = subCategory.Description,

				Images = subCategory.Images
			.Where(img => img.DeletedAt == null)
			.Select(img => new ImageDto
			{
				Id = img.Id,
				IsMain = img.IsMain,
				Url = img.Url
			}).ToList(),

				Products = subCategory.Products.Where(p =>IsAdmin||( p.IsActive && p.DeletedAt == null)).Select(p => new ProductDto
				{

					Id = p.Id,
					Name = p.Name,
					Description = p.Description,
					AvailableQuantity = p.Quantity,
					Gender = p.Gender,
					SubCategoryId = p.SubCategoryId,
					Price = p.Price,
					CreatedAt = p.CreatedAt,
					ModifiedAt = p.ModifiedAt,
					DeletedAt = p.DeletedAt,
					FinalPrice = (p.Discount != null && p.Discount.IsActive && (p.Discount.DeletedAt == null) && (p.Discount.EndDate > DateTime.UtcNow)) ? Math.Round(p.Price - (((p.Discount.DiscountPercent) / 100) * p.Price)) : p.Price,
					fitType = p.fitType,
					images = p.Images.Where(i => i.DeletedAt == null).Select(i => new ImageDto { Id = i.Id, Url = i.Url }).ToList(),
					EndAt = (p.Discount != null && p.Discount.IsActive && p.Discount.EndDate > DateTime.UtcNow) && p.Discount.IsActive ? p.Discount.EndDate : null,
					DiscountName = (p.Discount != null && p.Discount.IsActive && p.Discount.EndDate > DateTime.UtcNow) ? p.Discount.Name : null,
					DiscountPrecentage = (p.Discount != null && p.Discount.IsActive && p.Discount.EndDate > DateTime.UtcNow) ? p.Discount.DiscountPercent : 0,
					IsActive = p.IsActive,
				}).ToList()
			});
		}
		public SubCategoryDtoWithData MapToSubCategoryDtoWithData(SubCategory subCategory,bool IsAdmin=false)
		{
			return new SubCategoryDtoWithData
			{
				Id = subCategory.Id,
				Name = subCategory.Name,
				IsActive = subCategory.IsActive,
				Images = subCategory.Images?.Where(i=>i.DeletedAt==null).Select(img => new ImageDto
				{
					Id = img.Id,
					Url = img.Url
				}).ToList(),
				Description = subCategory.Description,
				DeletedAt = subCategory.DeletedAt,
				CreatedAt = subCategory.CreatedAt,
				ModifiedAt = subCategory.ModifiedAt,
				Products = subCategory.Products?.Where(p => IsAdmin || (p.IsActive && p.DeletedAt == null)).Select(p => new ProductDto
				{
					Id = p.Id,
					Name = p.Name,
					IsActive = p.IsActive,
					AvailableQuantity = p.Quantity,
					Price = p.Price,
					Description = p.Description,
					SubCategoryId = p.SubCategoryId,
					CreatedAt = p.CreatedAt,
					DiscountPrecentage = (p.Discount != null && p.Discount.IsActive && (p.Discount.DeletedAt == null) && (p.Discount.EndDate > DateTime.UtcNow)) ? p.Discount.DiscountPercent : null,
					FinalPrice = (p.Discount != null && p.Discount.IsActive && (p.Discount.DeletedAt == null) && (p.Discount.EndDate > DateTime.UtcNow)) ? Math.Round(p.Price - ((p.Discount.DiscountPercent / 100) * p.Price)) : p.Price,

					DiscountName = (p.Discount != null && p.Discount.IsActive && (p.Discount.DeletedAt == null) && (p.Discount.EndDate > DateTime.UtcNow)) ? p.Discount.Name : null,
					EndAt = (p.Discount != null && p.Discount.IsActive && (p.Discount.DeletedAt == null) && (p.Discount.EndDate > DateTime.UtcNow)) ? p.Discount.EndDate : null,
					fitType = p.fitType,
					Gender = p.Gender,

					ModifiedAt = p.ModifiedAt,
					DeletedAt = p.DeletedAt,


					images = p.Images?.Where(i=>i.DeletedAt==null).Select(img => new ImageDto
					{
						Id = img.Id,
						IsMain = img.IsMain,
						Url = img.Url
					})?.ToList()
				}).ToList()
			};
		}
	}
}

