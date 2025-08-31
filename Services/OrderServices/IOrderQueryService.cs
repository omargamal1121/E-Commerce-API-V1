using E_Commerce.DtoModels.OrderDtos;
using E_Commerce.Enums;

namespace E_Commerce.Services.Order
{
    public interface IOrderQueryService
    {
        Task<Result<OrderDto>> GetOrderByIdAsync(int orderId, string userId, bool isAdmin = false);
        Task<Result<OrderDto>> GetOrderByNumberAsync(string orderNumber, string userId, bool isAdmin = false);
        Task<Result<int?>> GetOrderCountByCustomerAsync(string userId);
        Task<Result<decimal>> GetTotalRevenueByCustomerAsync(string userId);
        Task<Result<decimal>> GetTotalRevenueByDateRangeAsync(DateTime startDate, DateTime endDate);
        Task<Result<int?>> GetTotalOrderCountAsync(OrderStatus? status);
        Task<Result<List<OrderListDto>>> FilterOrdersAsync(string? userId = null, bool? deleted = null, int page = 1, int pageSize = 10, OrderStatus? status = null);
    }
}
