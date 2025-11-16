using DomainLayer.Enums;
using DomainLayer.Models;
using StackExchange.Redis;
using Order = DomainLayer.Models.Order;

namespace ApplicationLayer.Interfaces
{
    public interface IOrderRepository : IRepository<Order>
    {
        Task<Order?> GetOrderByIdAsync(int orderId);
        public Task LockOrderForUpdateAsync(int id);
        public  Task<bool> IsExistByIdAndUserId(int orderid, string userid);
        public  Task<bool> IsExistByOrderNumberAndUserIdAsync(string ordernumber, string userid);
        public  Task<bool> IsExistByOrderNumberAsync(string ordernumber);

        Task<Order?> GetOrderByNumberAsync(string orderNumber);
        Task<bool> UpdateOrderStatusAsync(int orderId, OrderStatus status, string? notes = null);
        Task<string> GenerateOrderNumberAsync();
        Task<int> GetOrderCountByCustomerAsync(string customerId);
        Task<decimal> GetTotalRevenueByCustomerAsync(string customerId);
        Task<decimal> GetTotalRevenueByDateRangeAsync(DateTime startDate, DateTime endDate);
     
        Task<int> GetTotalOrderCountAsync(OrderStatus? status = null);
    }
}

