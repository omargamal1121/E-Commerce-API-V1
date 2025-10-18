using E_Commerce.DtoModels.ImagesDtos;
using E_Commerce.DtoModels.ProductDtos;
using E_Commerce.Models;

namespace E_Commerce.Services.WishlistServices
{
    public interface IWishListMapper
    {
        IQueryable<WishlistItemDto> MapToWishlistItemDto(IQueryable<WishlistItem> query);
    }
    public class WishListMapper: IWishListMapper
    {
        public IQueryable<WishlistItemDto> MapToWishlistItemDto(
            IQueryable<WishlistItem> query)
        {

            var query1 = query
                 .Select(w => new WishlistItemDto
                 {
                     Id = w.Id,
                     CreatedAt = w.CreatedAt,
                     ModifiedAt = w.ModifiedAt,
                     ProductId = w.ProductId,
                     UserId = w.CustomerId,
                     Product = new ProductDto
                     {
                         Id = w.Product.Id,
                         Name = w.Product.Name,
                         Price = w.Product.Price,
                         IsActive = w.Product.IsActive,
                         DiscountPrecentage =
                             (w.Product.Discount != null &&
                              w.Product.Discount.IsActive &&
                              w.Product.Discount.DeletedAt == null &&
                              w.Product.Discount.EndDate > DateTime.UtcNow)
                                 ? w.Product.Discount.DiscountPercent
                                 : 0,
                         FinalPrice =
                             (w.Product.Discount != null &&
                              w.Product.Discount.IsActive &&
                              w.Product.Discount.DeletedAt == null &&
                              w.Product.Discount.EndDate > DateTime.UtcNow)
                                 ? Math.Round(w.Product.Price - ((w.Product.Discount.DiscountPercent / 100) * w.Product.Price))
                                 : w.Product.Price,
                         images = w.Product.Images
                             .Where(img => img.DeletedAt == null)
                             .Select(img => new ImageDto
                             {
                                 Url = img.Url,
                                 IsMain = img.IsMain
                             })
                     }
                 });

         

            return query1;
        }
    }
}
