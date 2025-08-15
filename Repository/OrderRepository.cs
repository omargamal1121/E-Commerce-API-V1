using E_Commerce.Context;
using E_Commerce.DtoModels.OrderDtos;
using E_Commerce.DtoModels.ProductDtos;
using E_Commerce.Enums;
using E_Commerce.Interfaces;
using E_Commerce.Models;
using E_Commerce.Services;
using E_Commerce.Services.PaymentMethodsServices;
using E_Commerce.Services.PaymentProvidersServices;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;
using System.Linq.Expressions;
using Order = E_Commerce.Models.Order;

namespace E_Commerce.Repository
{
    public class OrderRepository : MainRepository<Order>, IOrderRepository
    {
        private readonly AppDbContext _context;
        private readonly ILogger<OrderRepository> _logger;

        public OrderRepository(AppDbContext context, ILogger<OrderRepository> logger) 
            : base(context, logger)
        {
            _context = context;
            _logger = logger;
        }

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
					IsActive= item.Product.IsActive,
					FinalPrice = (item.Product.Discount != null && item.Product.Discount.IsActive && (item.Product.Discount.DeletedAt == null) && (item.Product.Discount.EndDate > DateTime.UtcNow)) ? Math.Round(item.Product.Price - (((item.Product.Discount.DiscountPercent) / 100) * item.Product.Price)) : item.Product.Price,
					DiscountPrecentage = (item.Product.Discount != null && item.Product.Discount.IsActive && item.Product.Discount.EndDate > DateTime.UtcNow) ? item.Product.Discount.DiscountPercent : 0,

					MainImageUrl = item.Product.Images.FirstOrDefault(img => img.DeletedAt==null).Url??string.Empty,
					productVariantForCartDto = new ProductVariantForCartDto
					{
						Id = item.ProductVariantId,
						Color = item.ProductVariant.Color ,
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

			Payment = order.Payment == null ? null : new PaymentDto
			{
				Id = order.Payment.Id,
				CreatedAt = order.Payment.CreatedAt,
				ModifiedAt = order.Payment.ModifiedAt,
				CustomerId = order.Payment.CustomerId,
				PaymentMethodId = order.Payment.PaymentMethodId,
				PaymentMethod = order.Payment.PaymentMethod == null ? null : new PaymentMethodDto
				{
					Id = order.Payment.PaymentMethod.Id,
					CreatedAt = order.Payment.PaymentMethod.CreatedAt,
					ModifiedAt = order.Payment.PaymentMethod.ModifiedAt,
					Name = order.Payment.PaymentMethod.Name,
                   
				},
				PaymentProviderId = order.Payment.PaymentProviderId,
				PaymentProvider = order.Payment.PaymentProvider == null ? null : new PaymentProviderDto
				{
					Id = order.Payment.PaymentProvider.Id,
					CreatedAt = order.Payment.PaymentProvider.CreatedAt.Value,
					UpdatedAt = order.Payment.PaymentProvider.ModifiedAt,
					Name = order.Payment.PaymentProvider.Name,
				
	
              
                    
				},
				Amount = order.Payment.Amount,
				PaymentDate = order.Payment.PaymentDate,
				OrderId = order.Payment.OrderId,
			
                DeletedAt= order.Payment.DeletedAt,
                
			}
		};

		



		public async Task<Order?> GetOrderByIdAsync(int orderId)
        {
            _logger.LogInformation($"Getting order by ID: {orderId}");

            var order = await _context.Orders
                .FirstOrDefaultAsync(o => o.Id == orderId);
            return order;
        }

        public async Task<Order?> GetOrderByNumberAsync(string orderNumber)
        {
            _logger.LogInformation($"Getting order by number: {orderNumber}");

            var order = await _context.Orders
                .FirstOrDefaultAsync(o => o.OrderNumber == orderNumber);
            return order;
        }

      
 

        public async Task<bool> UpdateOrderStatusAsync(int orderId, OrderStatus status, string? notes = null)
        {
            _logger.LogInformation($"Updating order {orderId} status to {status}");
            
            try
            {
                var order = await _context.Orders.FirstOrDefaultAsync(o => o.Id == orderId && o.DeletedAt == null);

                if (order == null)
                {
                    _logger.LogWarning($"Order {orderId} not found");
                    return false;
                }

                order.Status = status;
                order.ModifiedAt = DateTime.UtcNow;

                switch (status)
                {
                    case OrderStatus.Shipped:
                        order.ShippedAt = DateTime.UtcNow;
                        break;
                    case OrderStatus.Delivered:
                        order.DeliveredAt = DateTime.UtcNow;
                        break;
                    case OrderStatus.Cancelled:
                        order.CancelledAt = DateTime.UtcNow;
                        break;
                }

                if (!string.IsNullOrWhiteSpace(notes))
                {
                    order.Notes = notes;
                }

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error updating order status: {ex.Message}");
                return false;
            }
        }
        public async Task<bool> IsExistByIdAndUserId(int orderid,string userid)
        {
            _logger.LogInformation($"Checking if order {orderid} exists for user {userid}");
            
            return await _context.Orders.AnyAsync(o => o.Id == orderid && o.CustomerId == userid && o.DeletedAt == null);
        }
        public async Task<bool> IsExistByOrderNumberAndUserIdAsync(string ordernumber,string userid)
        {
            _logger.LogInformation($"Checking if order {ordernumber} exists for user {userid}");
            
            return await _context.Orders.AnyAsync(o => o.OrderNumber == ordernumber && o.CustomerId == userid && o.DeletedAt == null);
        }
        public async Task<bool> IsExistByOrderNumberAsync(string ordernumber)
        {
            _logger.LogInformation($"Checking if order {ordernumber} exists for user");
            
            return await _context.Orders.AnyAsync(o => o.OrderNumber == ordernumber && o.DeletedAt == null);
        }
        public async Task<bool> CancelOrderAsync(int orderId, string cancellationReason)
        {
            _logger.LogInformation($"Cancelling order {orderId}");
            
            try
            {
                var order = await _context.Orders
              
                    .FirstOrDefaultAsync(o => o.Id == orderId && o.DeletedAt == null);

                if (order == null)
                {
                    _logger.LogWarning($"Order {orderId} not found");
                    return false;
                }

                if (!order.CanBeCancelled)
                {
                    _logger.LogWarning($"Order {orderId} cannot be cancelled in current status: {order.Status}");
                    return false;
                }

                order.Status = OrderStatus.Cancelled;
                order.CancelledAt = DateTime.UtcNow;
                order.ModifiedAt = DateTime.UtcNow;
                order.Notes = cancellationReason;

            
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error cancelling order: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> ShipOrderAsync(int orderId)
        {
            return await UpdateOrderStatusAsync(orderId, OrderStatus.Shipped);
        }

        public async Task<bool> DeliverOrderAsync(int orderId)
        {
            return await UpdateOrderStatusAsync(orderId, OrderStatus.Delivered);
        }

        public async Task<string> GenerateOrderNumberAsync()
        {
            var timestamp = DateTime.UtcNow.ToString("yyyyMMddHHmmss");
            var random = new Random();
            var randomPart = random.Next(1000, 9999).ToString();
            var orderNumber = $"ORD-{timestamp}-{randomPart}";

            // Ensure uniqueness
            while (await _context.Orders.AnyAsync(o => o.OrderNumber == orderNumber))
            {
                randomPart = random.Next(1000, 9999).ToString();
                orderNumber = $"ORD-{timestamp}-{randomPart}";
            }

            return orderNumber;
        }

        public async Task<int> GetOrderCountByCustomerAsync(string customerId)
        {
            return await _context.Orders
                .Where(o => o.CustomerId == customerId && o.DeletedAt == null)
                .CountAsync();
        }

        public async Task<decimal> GetTotalRevenueByCustomerAsync(string customerId)
        {
            return await _context.Orders
                .Where(o => o.CustomerId == customerId && o.DeletedAt == null && o.Status != OrderStatus.Cancelled)
                .SumAsync(o => o.Total);
        }

        public async Task<decimal> GetTotalRevenueByDateRangeAsync(DateTime startDate, DateTime endDate)
        {
            return await _context.Orders
                .Where(o => o.CreatedAt >= startDate && o.CreatedAt <= endDate && o.DeletedAt == null && o.Status != OrderStatus.Cancelled)
                .SumAsync(o => o.Total);
        }

        public async Task<List<Order>> FilterOrderAsync(string? userid=null,bool? Deleted=null ,int page=1, int pageSize=10, OrderStatus? status = null)
        {
            var query = GetAll();
            if (userid != null)
                query.Where(o => o.CustomerId == userid);
            if(Deleted.HasValue)
            {
                if (Deleted.Value)
                    query = query.Where(o => o.DeletedAt != null);
                else
                    query = query.Where(o => o.DeletedAt == null);
            }
            if (status.HasValue)
                query = query.Where(o => o.Status == status.Value);
                

            return await query
                .OrderByDescending(o => o.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();
        }

        public async Task<int> GetTotalOrderCountAsync(OrderStatus? status = null)
        {
            var query = _context.Orders.Where(o => o.DeletedAt == null);

            if (status.HasValue)
            {
                query = query.Where(o => o.Status == status.Value);
            }

            return await query.CountAsync();
        }
    }
} 