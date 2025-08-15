using E_Commerce.Enums;
using E_Commerce.Models;
using StackExchange.Redis;
using Order = E_Commerce.Models.Order;

namespace E_Commerce.Interfaces
{
    public interface IOrderRepository : IRepository<Order>
    {
        Task<Order?> GetOrderByIdAsync(int orderId);
        public  Task<bool> IsExistByIdAndUserId(int orderid, string userid);
        public  Task<bool> IsExistByOrderNumberAndUserIdAsync(string ordernumber, string userid);
        public  Task<bool> IsExistByOrderNumberAsync(string ordernumber);

        Task<Order?> GetOrderByNumberAsync(string orderNumber);
        Task<bool> UpdateOrderStatusAsync(int orderId, OrderStatus status, string? notes = null);
        Task<bool> CancelOrderAsync(int orderId, string cancellationReason);
        Task<bool> ShipOrderAsync(int orderId);
        Task<bool> DeliverOrderAsync(int orderId);
        Task<string> GenerateOrderNumberAsync();
        Task<int> GetOrderCountByCustomerAsync(string customerId);
        Task<decimal> GetTotalRevenueByCustomerAsync(string customerId);
        Task<decimal> GetTotalRevenueByDateRangeAsync(DateTime startDate, DateTime endDate);
        public Task<List<Order>> FilterOrderAsync(string? userid = null, bool? Deleted = null, int page = 1, int pageSize = 10, OrderStatus? status = null);

        Task<int> GetTotalOrderCountAsync(OrderStatus? status = null);
    }
}