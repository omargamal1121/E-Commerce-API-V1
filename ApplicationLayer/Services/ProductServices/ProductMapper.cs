using ApplicationLayer.DtoModels.DiscoutDtos;
using ApplicationLayer.DtoModels.ImagesDtos;
using ApplicationLayer.DtoModels.ProductDtos;
using DomainLayer.Models;
using static ApplicationLayer.Services.ProductServices.ProductSearchService;

namespace ApplicationLayer.Services.ProductServices
{
    public class ProductMapper : IproductMapper
    {
      
        private static bool IsDiscountValidForUser(Discount? d)
            => d != null && d.IsActive && d.DeletedAt == null && d.EndDate > DateTime.UtcNow;



        public ProductDto Maptoproductdto(Product p, bool isAdmin = false)
        {
            var validDiscountForUser = IsDiscountValidForUser(p.Discount);

            decimal finalPrice;
            if (isAdmin)
            {

                finalPrice = (p.Discount != null)
                    ? Math.Round(p.Price - ((p.Discount.DiscountPercent / 100m) * p.Price))
                    : p.Price;
            }
            else
            {

                finalPrice = validDiscountForUser
                    ? Math.Round(p.Price - ((p.Discount!.DiscountPercent / 100m) * p.Price))
                    : p.Price;
            }

            return new ProductDto
            {
                Id = p.Id,
                Name = p.Name,
                IsActive = p.IsActive,
                AvailableQuantity = p.Quantity,
                Price = p.Price,
                Description = p.Description,
                SubCategoryId = p.SubCategoryId,
                CreatedAt = p.CreatedAt,
                ModifiedAt = p.ModifiedAt,
                DeletedAt = p.DeletedAt,
                fitType = p.fitType,
                Gender = p.Gender,

                FinalPrice = finalPrice,
                EndAt = isAdmin ? p.Discount?.EndDate :
                                  (validDiscountForUser ? p.Discount!.EndDate : null),
                DiscountPrecentage = isAdmin ? p.Discount?.DiscountPercent ?? 0 :
                                               (validDiscountForUser ? p.Discount!.DiscountPercent : 0),
                DiscountName = isAdmin ? p.Discount?.Name :
                                         (validDiscountForUser ? p.Discount!.Name : null),
                images = p.Images
                          .Where(i => i.DeletedAt == null)
                          .Select(i => new ImageDto
                          {
                              Id = i.Id,
                              IsMain = i.IsMain,
                              Url = i.Url
                          })
            };
        }

		public IQueryable<ProductDetailDto> maptoProductDetailDtoexpression(
		IQueryable<Product> query,
		bool isAdmin = false)
		{
			var now = DateTime.UtcNow;

			return query
				.Select(p => new
				{
					Product = p,
					ValidDiscount =
						p.Discount != null &&
						p.Discount.IsActive &&
						p.Discount.DeletedAt == null &&
						p.Discount.EndDate > now
				})
				.Select(x => new ProductDetailDto
				{
					Id = x.Product.Id,
					Name = x.Product.Name,
					Description = x.Product.Description,
					AvailableQuantity = x.Product.Quantity,
					Gender = x.Product.Gender,
					CreatedAt = x.Product.CreatedAt,
					DeletedAt = x.Product.DeletedAt,
					ModifiedAt = x.Product.ModifiedAt,
					fitType = x.Product.fitType,
					IsActive = x.Product.IsActive,
					Price = x.Product.Price,
					SubCategoryId = x.Product.SubCategoryId,
					SubCategoryName = x.Product.SubCategory.Name,

					FinalPrice =
						x.ValidDiscount
						? x.Product.Price - ((x.Product.Discount!.DiscountPercent / 100m) * x.Product.Price)
						: x.Product.Price,

					Discount =
						isAdmin
						? (x.Product.Discount != null
							? new DiscountDto
							{
								Id = x.Product.Discount.Id,
								DiscountPercent = x.Product.Discount.DiscountPercent,
								IsActive = x.Product.Discount.IsActive,
								StartDate = x.Product.Discount.StartDate,
								EndDate = x.Product.Discount.EndDate,
								Name = x.Product.Discount.Name,
								Description = x.Product.Discount.Description
							}
							: null)
						: (x.ValidDiscount
							? new DiscountDto
							{
								Id = x.Product.Discount!.Id,
								DiscountPercent = x.Product.Discount.DiscountPercent,
								IsActive = x.Product.Discount.IsActive,
								StartDate = x.Product.Discount.StartDate,
								EndDate = x.Product.Discount.EndDate,
								Name = x.Product.Discount.Name,
								Description = x.Product.Discount.Description
							}
							: null),

					Images = x.Product.Images
						.Where(i => i.DeletedAt == null)
						.Select(i => new ImageDto
						{
							Id = i.Id,
							Url = i.Url
						}),

					Variants = (isAdmin
						? x.Product.ProductVariants
						: x.Product.ProductVariants.Where(v => v.DeletedAt == null && v.Quantity > 0))
						.Select(v => new ProductVariantDto
						{
							Id = v.Id,
							Color = v.Color,
							Size = v.Size,
							Waist = v.Waist,
							Length = v.Length,
							Quantity = v.Quantity,
							ProductId = v.ProductId
						})
				});
		}
		public IQueryable<ProductDto> maptoProductDtoexpression(IQueryable<Product> query, bool IsAdmin = false)
		{
			var now = DateTime.UtcNow;

			return query.Select(p => new
			{
				Product = p,
				ValidDiscount =
					p.Discount != null &&
					p.Discount.IsActive &&
					p.Discount.DeletedAt == null &&
					p.Discount.EndDate > now
			})
			.Select(x => new ProductDto
			{
				Id = x.Product.Id,
				Name = x.Product.Name,
				Description = x.Product.Description,
				AvailableQuantity = x.Product.Quantity,
				Gender = x.Product.Gender,
				SubCategoryId = x.Product.SubCategoryId,
				Price = x.Product.Price,
				CreatedAt = x.Product.CreatedAt,
				ModifiedAt = x.Product.ModifiedAt,
				DeletedAt = x.Product.DeletedAt,
				fitType = x.Product.fitType,
				IsActive = x.Product.IsActive,

				FinalPrice =
					
					 (x.ValidDiscount
						? x.Product.Price - ((x.Product.Discount!.DiscountPercent / 100m) * x.Product.Price)
						: x.Product.Price),

				EndAt =
					IsAdmin
					? (x.Product.Discount != null ? x.Product.Discount.EndDate : null)
					: (x.ValidDiscount ? x.Product.Discount!.EndDate : null),

				DiscountName =
					IsAdmin
					? (x.Product.Discount != null ? x.Product.Discount.Name : null)
					: (x.ValidDiscount ? x.Product.Discount!.Name : null),

				DiscountPrecentage =
					IsAdmin
					? (x.Product.Discount != null ? x.Product.Discount.DiscountPercent : 0)
					: (x.ValidDiscount ? x.Product.Discount!.DiscountPercent : 0),

				images = x.Product.Images
					.Where(i => i.DeletedAt == null)
					.Select(i => new ImageDto
					{
						Id = i.Id,
						Url = i.Url,
						IsMain = i.IsMain
					})
			});
		}


	}
}


