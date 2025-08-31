using E_Commerce.DtoModels.DiscoutDtos;
using E_Commerce.DtoModels.ImagesDtos;
using E_Commerce.DtoModels.ProductDtos;
using E_Commerce.Models;

namespace E_Commerce.Services.ProductServices
{
	public class ProductMapper : IproductMapper
	{
		public ProductDto Maptoproductdto(E_Commerce.Models.Product p)
		{
			var productdto = new ProductDto
			{
				Id = p.Id,
				Name = p.Name,
				IsActive = p.IsActive,
				AvailableQuantity = p.Quantity,
				Price = p.Price,
				Description = p.Description,
				SubCategoryId = p.SubCategoryId,
				CreatedAt = p.CreatedAt,
				FinalPrice = (p.Discount != null && p.Discount.IsActive && (p.Discount.DeletedAt == null) && (p.Discount.EndDate > DateTime.UtcNow)) ? Math.Round(p.Price - (((p.Discount.DiscountPercent) / 100) * p.Price)) : p.Price,

				fitType = p.fitType,
				Gender = p.Gender,
				ModifiedAt = p.ModifiedAt,
				DeletedAt = p.DeletedAt,
			};
			if (p.Discount != null && p.Discount.IsActive && (p.Discount.DeletedAt == null) && (p.Discount.EndDate > DateTime.UtcNow))
			{
				productdto.EndAt = p.Discount.EndDate;
				productdto.DiscountPrecentage = p.Discount.DiscountPercent;
				productdto.DiscountName = p.Discount.Name;
			}
			if (p.Images != null)
				productdto.images = p.Images.Where(i => i.DeletedAt == null).Select(i => new ImageDto
				{
					Id = i.Id,
					IsMain = i.IsMain,
					Url = i.Url
				});


			return productdto;



		}

		public IQueryable<ProductDetailDto> maptoProductDetailDtoexpression(IQueryable<Product> query)
		{
			return query.Select(p => new ProductDetailDto
			{
				Id = p.Id,
				Name = p.Name,
				Description = p.Description,
				AvailableQuantity = p.Quantity,
				Gender = p.Gender,
				CreatedAt = p.CreatedAt,
				DeletedAt = p.DeletedAt,
				ModifiedAt = p.ModifiedAt,
				fitType = p.fitType,
				IsActive = p.IsActive,
				FinalPrice = (p.Discount != null && p.Discount.IsActive && (p.Discount.DeletedAt == null) && (p.Discount.EndDate > DateTime.UtcNow)) ? Math.Round(p.Price - (((p.Discount.DiscountPercent) / 100) * p.Price)) : p.Price,

				Price = p.Price,
				SubCategoryId = p.SubCategoryId,
				Discount = p.Discount != null && p.Discount.DeletedAt == null && p.Discount.EndDate > DateTime.UtcNow ? new DiscountDto
				{
					Id = p.Discount.Id,
					DiscountPercent = p.Discount.DiscountPercent,
					IsActive = p.Discount.IsActive,
					StartDate = p.Discount.StartDate,
					EndDate = p.Discount.EndDate,
					Name = p.Discount.Name,
					Description = p.Discount.Description
				} : null,
				Images = p.Images.Where(i => i.DeletedAt == null).Select(i => new ImageDto
				{
					Id = i.Id,
					Url = i.Url
				}).ToList(),
				Variants = p.ProductVariants.Where(v => v.DeletedAt == null && v.Quantity != 0).Select(v => new ProductVariantDto
				{
					Id = v.Id,
					Color = v.Color,
					Size = v.Size,
					Waist = v.Waist,
					Length = v.Length,
					Quantity = v.Quantity,
					ProductId = v.ProductId
				}).ToList()
			});



		}

		public IQueryable<ProductDto> maptoProductDtoexpression(IQueryable<Product> query)
		{
			return query.Select(p => new ProductDto
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
				fitType = p.fitType,
				IsActive = p.IsActive,


				FinalPrice = (p.Discount != null &&
						  p.Discount.IsActive &&
						  p.Discount.DeletedAt == null &&
						  p.Discount.EndDate > DateTime.UtcNow)
						  ? p.Price - ((p.Discount.DiscountPercent / 100m) * p.Price)
						  : p.Price,


				EndAt = (p.Discount != null &&
					 p.Discount.IsActive &&
					 p.Discount.DeletedAt == null &&
					 p.Discount.EndDate > DateTime.UtcNow)
					 ? p.Discount.EndDate
					 : null,


				DiscountName = (p.Discount != null &&
							p.Discount.IsActive &&
							p.Discount.DeletedAt == null &&
							p.Discount.EndDate > DateTime.UtcNow)
							? p.Discount.Name
							: null,

				DiscountPrecentage = (p.Discount != null &&
								  p.Discount.IsActive &&
								  p.Discount.DeletedAt == null &&
								  p.Discount.EndDate > DateTime.UtcNow)
								  ? p.Discount.DiscountPercent
								  : 0,


				images = p.Images

				.Select(i => new ImageDto
				{
					Id = i.Id,
					Url = i.Url
				})
			});
		}
	}
}
