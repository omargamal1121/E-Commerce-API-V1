using System.Linq.Expressions;
using ApplicationLayer.DtoModels.CustomerAddressDtos;
using ApplicationLayer.DtoModels.OrderDtos;
using ApplicationLayer.DtoModels.ProductDtos;
using DomainLayer.Enums;
using DomainLayer.Models;

namespace ApplicationLayer.Services.OrderService
{
    public class OrderMapper : IOrderMapper
    {
        public static readonly Expression<Func<Order, OrderListDto>> OrderListSelector = o => new OrderListDto
        {
            Id = o.Id,
            OrderNumber = o.OrderNumber,
            CustomerName = o.Customer.Name,
            Status = o.Status.ToString(),
            Total = o.Total,
            CreatedAt = o.CreatedAt??default,
            UpdatedAt = o.ModifiedAt,
            canBeCancelled = o.Status == OrderStatus.PendingPayment
                  || o.Status == OrderStatus.Processing,
            itemCount = o.Items.Sum(i => i.Quantity),
            
            PaymentStatus = o.Payment
        .Select(p => (PaymentStatus?)p.Status)
        .FirstOrDefault() ?? PaymentStatus.Pending,

            paymentMethod = o.Payment
        .Select(p => p.PaymentMethod.Name)
        .FirstOrDefault() ?? "N/A",

                        imageurl = o.Items
        .SelectMany(i => i.Product.Images
            .Where(img => img.DeletedAt == null)
            .Select(img => img.Url))
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

			// ✅ بدون null check على الـ collection
			Payment = order.Payment.Select(p => new PaymentDto
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

			Customer = order.Customer == null ? null : new CustomerDto
			{
				Id = order.Customer.Id,
				FullName = order.Customer.Name,
				Email = order.Customer.Email,
				PhoneNumber = order.Customer.PhoneNumber,
				customerAddress = new CustomerAddressDto
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

				
					FinalPrice = item.Product.Discount != null
						&& item.Product.Discount.IsActive
						&& item.Product.Discount.DeletedAt == null
						&& item.Product.Discount.EndDate > DateTime.UtcNow
							? Math.Round(item.Product.Price - (item.Product.Discount.DiscountPercent /(decimal) 100.0 * item.Product.Price))
							: item.Product.Price,

					DiscountPrecentage = item.Product.Discount != null
						&& item.Product.Discount.IsActive
						&& item.Product.Discount.DeletedAt == null
						&& item.Product.Discount.EndDate > DateTime.UtcNow
							? item.Product.Discount.DiscountPercent
							: 0, 

					// ✅ آمن من NullReferenceException
					MainImageUrl = item.Product.Images
						.Where(img => img.DeletedAt == null)
						.Select(img => img.Url)
						.FirstOrDefault() ?? string.Empty,

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
		Expression<Func<Order, OrderListDto>> IOrderMapper.OrderListSelector => OrderListSelector;
        Expression<Func<Order, OrderDto>> IOrderMapper.OrderSelector => OrderSelector;
    }
}


