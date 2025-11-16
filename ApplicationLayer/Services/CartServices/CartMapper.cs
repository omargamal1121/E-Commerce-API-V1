using ApplicationLayer.DtoModels.CartDtos;
using DomainLayer.Models;
using System.Linq.Expressions;

namespace ApplicationLayer.Services.CartServices
{
    public class CartMapper : ICartMapper
    {
        public Expression<Func<Cart, CartDto>> CartDtoSelector => cart => new CartDto
        {
            Id = cart.Id,
            UserId = cart.UserId,
            TotalItems = cart.Items.Count,
            CheckoutDate = cart.CheckoutDate,
            CreatedAt = cart.CreatedAt,

            Items = cart.Items
                .Where(item => item.Product.IsActive && item.Product.DeletedAt == null)
                .Select(item => new CartItemDto
                {
                    Id = item.Id,
                    ProductId = item.ProductId,
                    Quantity = item.Quantity,
                    AddedAt = item.AddedAt,

                    Product = new DtoModels.ProductDtos.ProductForCartDto
                    {
                        Id = item.Product.Id,
                        Name = item.Product.Name,
                        Price = item.Product.Price,

                        FinalPrice =
                            item.Product.Discount != null
                            && item.Product.Discount.IsActive
                            && item.Product.Discount.DeletedAt == null
                            && item.Product.Discount.EndDate > DateTime.UtcNow
                                ? item.Product.Price - item.Product.Discount.DiscountPercent / 100 * item.Product.Price
                                : item.Product.Price,

                        DiscountName =
                            item.Product.Discount != null
                            && item.Product.Discount.IsActive
                            && item.Product.Discount.DeletedAt == null
                            && item.Product.Discount.EndDate > DateTime.UtcNow
                                ? item.Product.Discount.Name
                                : null,

                        DiscountPrecentage =
                            item.Product.Discount != null
                            && item.Product.Discount.IsActive
                            && item.Product.Discount.DeletedAt == null
                            && item.Product.Discount.EndDate > DateTime.UtcNow
                                ? item.Product.Discount.DiscountPercent
                                : 0,

                        MainImageUrl = item.Product.Images
                            .Where(img => img.IsMain && img.DeletedAt == null)
                            .Select(img => img.Url)
                            .FirstOrDefault()
                            ?? item.Product.Images
                            .Where(img => img.DeletedAt == null)
                            .Select(img => img.Url)
                            .FirstOrDefault(),

                        IsActive = item.Product.IsActive,

                        productVariantForCartDto = new DtoModels.ProductDtos.ProductVariantForCartDto
                        {
                            Color = item.ProductVariant.Color,
                            Id = item.ProductVariant.Id,
                            CreatedAt = item.ProductVariant.CreatedAt,
                            DeletedAt = item.ProductVariant.DeletedAt,
                            ModifiedAt = item.ProductVariant.ModifiedAt,
                            Size = item.ProductVariant.Size,
                            Waist = item.ProductVariant.Waist,
                            Length = item.ProductVariant.Length,
                            Quantity = item.ProductVariant.Quantity
                        }
                    },
                    UnitPrice = item.UnitPrice,
                }).ToList()
        };

        public CartDto ToCartDto(Cart cart)
        {
            return CartDtoSelector.Compile()(cart);
        }
    }
}


