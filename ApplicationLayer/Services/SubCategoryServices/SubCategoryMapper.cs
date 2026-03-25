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
				CategoryId = subCategory.CategoryId,
				CategoryName	= subCategory.Category.Name,
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
				CategoryName = subCategory.Category.Name,
				CategoryId	= subCategory.CategoryId,
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

		public IQueryable<SubCategoryDtoWithData> SubCategorySelectorWithData(
	IQueryable<SubCategory> subCategories,
	bool IsAdmin = false)
		{
			var now = DateTime.UtcNow;

			return subCategories
				.Select(sc => new
				{
					SubCategory = sc
				})
				.Select(x => new SubCategoryDtoWithData
				{
					Id = x.SubCategory.Id,
					Name = x.SubCategory.Name,
					CategoryId = x.SubCategory.CategoryId,
					CategoryName = x.SubCategory.Category.Name,
					IsActive = x.SubCategory.IsActive,
					CreatedAt = x.SubCategory.CreatedAt,
					ModifiedAt = x.SubCategory.ModifiedAt,
					DeletedAt = x.SubCategory.DeletedAt,
					Description = x.SubCategory.Description,

					Images = x.SubCategory.Images
						.Where(img => img.DeletedAt == null)
						.Select(img => new ImageDto
						{
							Id = img.Id,
							Url = img.Url,
							IsMain = img.IsMain
						}),

					Products = x.SubCategory.Products
						.Where(p => IsAdmin || (p.IsActive && p.DeletedAt == null))
						.Select(p => new
						{
							Product = p,
							ValidDiscount =
								p.Discount != null &&
								p.Discount.IsActive &&
								p.Discount.DeletedAt == null &&
								p.Discount.EndDate > now
						})
						.Select(p => new ProductDto
						{
							Id = p.Product.Id,
							Name = p.Product.Name,
							Price = p.Product.Price,
							AvailableQuantity = p.Product.Quantity,

							FinalPrice = p.ValidDiscount
								? p.Product.Price - ((p.Product.Discount!.DiscountPercent / 100m) * p.Product.Price)
								: p.Product.Price,

							DiscountName = p.ValidDiscount ? p.Product.Discount!.Name : null,
							DiscountPrecentage = p.ValidDiscount ? p.Product.Discount!.DiscountPercent : 0,
							EndAt = p.ValidDiscount ? p.Product.Discount!.EndDate : null,

							images = p.Product.Images
								.Where(i => i.DeletedAt == null)
								.Select(i => new ImageDto
								{
									Id = i.Id,
									Url = i.Url
								})
						})
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
				}),
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
					})
				}).ToList()
			};
		}
	}
}

