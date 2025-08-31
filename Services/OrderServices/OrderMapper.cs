using System.Linq.Expressions;
using E_Commerce.DtoModels.OrderDtos;
using E_Commerce.DtoModels.ProductDtos;
using E_Commerce.Models;

namespace E_Commerce.Services.Order
{
    public class OrderMapper : IOrderMapper
    {
        public static readonly Expression<Func<Models.Order, OrderListDto>> OrderListSelector = o => new OrderListDto
        {
            Id = o.Id,
            OrderNumber = o.OrderNumber,
            CustomerName = o.Customer.Name,
            Status = o.Status.ToString(),
            Total = o.Total,
            CreatedAt = o.CreatedAt.Value,
        };

        public static readonly Expression<Func<E_Commerce.Models.Order, OrderDto>> OrderSelector = order => new OrderDto
        {
            Id = order.Id,
            CreatedAt = order.CreatedAt,
            ModifiedAt = order.ModifiedAt,
            OrderNumber = order.OrderNumber,
            Status = order.Status.ToString(),
            Subtotal = order.Subtotal,
            TaxAmount = order.TaxAmount,
            ShippingCost = order.ShippingCost,
            DiscountAmount = order.DiscountAmount,
            Total = order.Total,
            Notes = order.Notes,
            DeletedAt = order.DeletedAt,
            ShippedAt = order.ShippedAt,
            DeliveredAt = order.DeliveredAt,
            CancelledAt = order.CancelledAt,

            Customer = order.Customer == null ? null : new CustomerDto
            {
                Id = order.Customer.Id,
                FullName = order.Customer.Name,
                Email = order.Customer.Email,
                PhoneNumber = order.Customer.PhoneNumber
            },

            Items = order.Items.Select(item => new OrderItemDto
            {
                Id = item.Id,
                CreatedAt = item.CreatedAt,
                ModifiedAt = item.ModifiedAt,
                Quantity = item.Quantity,
                UnitPrice = item.UnitPrice,
                TotalPrice = item.TotalPrice,
                OrderedAt = item.OrderedAt,
                Product = new ProductForCartDto
                {
                    Id = item.Product.Id,
                    Name = item.Product.Name,
                    Price = item.Product.Price,
                    IsActive = item.Product.IsActive,
                    FinalPrice = (item.Product.Discount != null && item.Product.Discount.IsActive && (item.Product.Discount.DeletedAt == null) && (item.Product.Discount.EndDate > DateTime.UtcNow)) ? Math.Round(item.Product.Price - (((item.Product.Discount.DiscountPercent) / 100) * item.Product.Price)) : item.Product.Price,
                    DiscountPrecentage = (item.Product.Discount != null && item.Product.Discount.IsActive && item.Product.Discount.EndDate > DateTime.UtcNow) ? item.Product.Discount.DiscountPercent : 0,

                    MainImageUrl = item.Product.Images.FirstOrDefault(img => img.DeletedAt == null).Url ?? string.Empty,
                    productVariantForCartDto = new ProductVariantForCartDto
                    {
                        Id = item.ProductVariantId,
                        Color = item.ProductVariant.Color,
                        CreatedAt = item.ProductVariant.CreatedAt ?? DateTime.MinValue,
                        ModifiedAt = item.ProductVariant.ModifiedAt ?? DateTime.MinValue,
                        Size = item.ProductVariant.Size,
                        DeletedAt = item.ProductVariant.DeletedAt,
                        Length = item.ProductVariant.Length ?? 0,
                        Quantity = item.Quantity,
                        Waist = item.ProductVariant.Waist ?? 0
                    }
                }
            }).ToList(),
        };

        Expression<Func<Models.Order, OrderListDto>> IOrderMapper.OrderListSelector => OrderListSelector;
        Expression<Func<Models.Order, OrderDto>> IOrderMapper.OrderSelector => OrderSelector;
    }
}
