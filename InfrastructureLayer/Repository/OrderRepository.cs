using ApplicationLayer.Interfaces;
using DomainLayer.Enums;
using DomainLayer.Models;
using InfrastructureLayer.Context;
using InfrastructureLayer.UOW;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;
using System.Linq.Expressions;
using Order = DomainLayer.Models.Order;
namespace InfrastructureLayer.Repository
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

	

        public async Task LockOrderForUpdateAsync(int id)
        {
            _logger.LogInformation("Locking orders table for update");
            await _context.Database.ExecuteSqlRawAsync(
                 "SELECT Id FROM Orders WHERE Id = {0} FOR UPDATE",
                 id);

        }

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
                    case OrderStatus.CancelledByAdmin:
                        order.CancelledAt = DateTime.UtcNow;
                        break;
                    case OrderStatus.CancelledByUser:
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
				.Where(o =>
					o.CustomerId == customerId &&
					o.DeletedAt == null &&
					(
						o.Status == OrderStatus.Complete ||
						o.Status == OrderStatus.Delivered ||
						o.Status == OrderStatus.Confirmed ||
						o.Status == OrderStatus.Processing ||
						o.Status == OrderStatus.Shipped
					)
				)
				.SumAsync(o => o.Total);
		}

		public async Task<decimal> GetTotalRevenueByDateRangeAsync(DateTime startDate, DateTime endDate)
        {
            return await _context.Orders
					.Where(o =>
					o.DeletedAt == null &&
					(
						o.Status == OrderStatus.Complete ||
						o.Status == OrderStatus.Delivered ||
						o.Status == OrderStatus.Confirmed ||
						o.Status == OrderStatus.Processing ||
						o.Status == OrderStatus.Shipped
					)
				)
				.SumAsync(o => o.Total);
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