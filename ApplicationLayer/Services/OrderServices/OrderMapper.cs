using System.Linq.Expressions;
using Application.DtoModels.CustomerAddressDtos;
using Application.DtoModels.OrderDtos;
using Application.DtoModels.ProductDtos;
using Domain.Enums;
using Domain.Models;

namespace Application.Services.OrderServices
{
    public class OrderMapper : IOrderMapper
    {
        public static readonly Expression<Func<Order, OrderListDto>> OrderListSelector = o => new OrderListDto
        {
            Id = o.Id,
            OrderNumber = o.OrderNumber,
            CustomerName = o.Customer != null ? o.Customer.Name : o.CustomerName,
            Status = o.Status.ToString(),
            Total = o.Total,
            CreatedAt = o.CreatedAt ?? default,
            UpdatedAt = o.ModifiedAt,
            canBeCancelled = o.Status == OrderStatus.PendingPayment
                          || o.Status == OrderStatus.Processing,
            itemCount = o.Items.Sum(i => i.Quantity),

            // Last payment attempt only
            PaymentStatus = o.Payment
                .OrderByDescending(p => p.CreatedAt)
                .ThenByDescending(p => p.Id)
                .Select(p => (PaymentStatus?)p.Status)
                .FirstOrDefault() ?? PaymentStatus.Pending,

            paymentMethod = o.Payment
                .OrderByDescending(p => p.CreatedAt)
                .ThenByDescending(p => p.Id)
                .Select(p => p.PaymentMethod.Name)
                .FirstOrDefault() ?? "N/A",

            // Pure snapshot — no navigation fallback
            imageurl = o.Items
                .Select(i => i.ProductImageUrl)
                .FirstOrDefault() ?? string.Empty,
        };

        public static readonly Expression<Func<Order, OrderDto>> OrderSelector = order => new OrderDto
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

            // Last payment attempt only
            Payment = order.Payment
                .OrderByDescending(p => p.CreatedAt)
                .ThenByDescending(p => p.Id)
                .Take(1)
                .Select(p => new PaymentDto
                {
                    Status = p.Status.ToString(),
                    Amount = p.Amount,
                    CreatedAt = p.CreatedAt,
                    Id = p.Id,
                    PaymentDate = p.PaymentDate,
                    PaymentMethodId = p.PaymentMethodId,
                    PaymentProviderId = p.PaymentProviderId,
                    PaymentMethod = p.PaymentMethod.Name,
                    ProviderOrderId = p.ProviderOrderId
                }),

            Customer = order.Customer == null
                ? (string.IsNullOrEmpty(order.CustomerName) ? null : new CustomerDto
                {
                    Id = string.Empty,
                    FullName = order.CustomerName,
                    Email = order.Email ?? string.Empty,
                    PhoneNumber = order.PhoneNumber ?? string.Empty,
                    customerAddress = new CustomerAddressDto
                    {
                        Id = 0,
                        City = order.City ?? string.Empty,
                        Country = "EG",
                        StreetAddress = ((order.Street ?? "") + " " + (order.Building ?? "")).Trim(),
                        PhoneNumber = order.PhoneNumber ?? string.Empty,
                    }
                })
                : new CustomerDto
                {
                    Id = order.Customer.Id,
                    FullName = order.Customer.Name,
                    Email = order.Customer.Email,
                    PhoneNumber = order.Customer.PhoneNumber,
                    customerAddress = order.CustomerAddress == null ? null : new CustomerAddressDto
                    {
                        Id = order.CustomerAddress.Id,
                        AdditionalNotes = order.CustomerAddress.AdditionalNotes,
                        AddressType = order.CustomerAddress.AddressType,
                        City = order.CustomerAddress.City,
                        Country = order.CustomerAddress.Country,
                        CreatedAt = order.CustomerAddress.CreatedAt,
                        CustomerId = order.CustomerAddress.CustomerId,
                        StreetAddress = order.CustomerAddress.StreetAddress,
                        State = order.CustomerAddress.State,
                        PhoneNumber = order.CustomerAddress.PhoneNumber,
                        IsDefault = order.CustomerAddress.IsDefault,
                        ModifiedAt = order.CustomerAddress.ModifiedAt,
                        PostalCode = order.CustomerAddress.PostalCode,
                    }
                },

            Items = order.Items.Select(item => new OrderItemDto
            {
                Id = item.Id,
                CreatedAt = item.CreatedAt,
                ModifiedAt = item.ModifiedAt,
                OrderedAt = item.OrderedAt,
                Quantity = item.Quantity,
                UnitPrice = item.UnitPrice,
                TotalPrice = item.TotalPrice,

                // Pure snapshot fields — never change after order is placed
                ProductName = item.ProductName ?? string.Empty,
                ImageUrl = item.ProductImageUrl,
                Color = item.Color,
                Size = item.Size,

                // Live availability flags only
                IsProductAvailable = item.Product != null && item.Product.IsActive,
                IsVariantAvailable = item.ProductVariant != null
                    && item.ProductVariant.IsActive
                    && item.ProductVariant.DeletedAt == null,
            }).ToList(),
        };

        Expression<Func<Order, OrderListDto>> IOrderMapper.OrderListSelector => OrderListSelector;
        Expression<Func<Order, OrderDto>> IOrderMapper.OrderSelector => OrderSelector;
    }
}
