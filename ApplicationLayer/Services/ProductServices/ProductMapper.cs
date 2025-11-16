using ApplicationLayer.DtoModels.DiscoutDtos;
using ApplicationLayer.DtoModels.ImagesDtos;
using ApplicationLayer.DtoModels.ProductDtos;
using DomainLayer.Models;

namespace ApplicationLayer.Services.ProductServices
{
    public class ProductMapper : IproductMapper
    {
      
        private static bool IsDiscountValidForUser(Discount? d)
            => d != null && d.IsActive && d.DeletedAt == null && d.EndDate > DateTime.UtcNow;


        private static DiscountDto? MapDiscount(Discount? d, bool isAdmin)
        {
            if (d == null) return null;

         
            if (isAdmin)
            {
                return new DiscountDto
                {
                    Id = d.Id,
                    DiscountPercent = d.DiscountPercent,
                    IsActive = d.IsActive,
                    StartDate = d.StartDate,
                    EndDate = d.EndDate,
                    Name = d.Name,
                    Description = d.Description
                };
            }

       
            if (IsDiscountValidForUser(d))
            {
                return new DiscountDto
                {
                    Id = d.Id,
                    DiscountPercent = d.DiscountPercent,
                    IsActive = d.IsActive,
                    StartDate = d.StartDate,
                    EndDate = d.EndDate,
                    Name = d.Name,
                    Description = d.Description
                };
            }

            return null;
        }

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

        public IQueryable<ProductDetailDto> maptoProductDetailDtoexpression(IQueryable<Product> query, bool isAdmin = false)
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
                Price = p.Price,
                SubCategoryId = p.SubCategoryId,

                FinalPrice = isAdmin
                    ? (p.Discount != null
                        ? p.Price - ((p.Discount.DiscountPercent / 100m) * p.Price)
                        : p.Price)
                    : (p.Discount != null && p.Discount.IsActive && p.Discount.DeletedAt == null && p.Discount.EndDate > DateTime.UtcNow
                        ? p.Price - ((p.Discount.DiscountPercent / 100m) * p.Price)
                        : p.Price),

                Discount = MapDiscount(p.Discount, isAdmin),
                Images = p.Images
                          .Where(i => i.DeletedAt == null)
                          .Select(i => new ImageDto
                          {
                              Id = i.Id,
                              Url = i.Url
                          }).ToList(),

                Variants = (isAdmin
                           ? p.ProductVariants
                           : p.ProductVariants.Where(v => v.DeletedAt == null && v.Quantity != 0))
                           .Select(v => new ProductVariantDto
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

        public IQueryable<ProductDto> maptoProductDtoexpression(IQueryable<Product> query, bool isAdmin = false)
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

                FinalPrice = isAdmin
                    ? (p.Discount != null
                        ? p.Price - ((p.Discount.DiscountPercent / 100m) * p.Price)
                        : p.Price)
                    : (p.Discount != null && p.Discount.IsActive && p.Discount.DeletedAt == null && p.Discount.EndDate > DateTime.UtcNow
                        ? p.Price - ((p.Discount.DiscountPercent / 100m) * p.Price)
                        : p.Price),

                EndAt = isAdmin
                    ? p.Discount!.EndDate
                    : (p.Discount != null && p.Discount.IsActive && p.Discount.DeletedAt == null && p.Discount.EndDate > DateTime.UtcNow
                        ? p.Discount.EndDate
                        : null),

                DiscountName = isAdmin
                    ? p.Discount!.Name
                    : (p.Discount != null && p.Discount.IsActive && p.Discount.DeletedAt == null && p.Discount.EndDate > DateTime.UtcNow
                        ? p.Discount.Name
                        : null),

                DiscountPrecentage = isAdmin
                    ? (p.Discount!.DiscountPercent )
                    : (p.Discount != null && p.Discount.IsActive && p.Discount.DeletedAt == null && p.Discount.EndDate > DateTime.UtcNow
                        ? p.Discount.DiscountPercent
                        : 0),

     
                images = p.Images
                          .Where(i => i.DeletedAt == null)
                          .Select(i => new ImageDto
                          {
                              Id = i.Id,
                              Url = i.Url
                          })
            });
        }
    }
}


