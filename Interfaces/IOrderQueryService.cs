using E_Commerce.DtoModels.OrderDtos;
using E_Commerce.DtoModels.Responses;
using E_Commerce.Enums;
using E_Commerce.Services;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace E_Commerce.Interfaces
{
    public interface IOrderQueryService
    {
        Task<Result<List<OrderListDto>>> FilterOrdersAsync(
          string? userId = null,
          bool? deleted = null,
          int page = 1,
          int pageSize = 10,
          OrderStatus? status = null);
        Task<Result<OrderDto>> GetOrderByIdAsync(int orderId, string userId, bool isAdmin = false);
        Task<Result<OrderDto>> GetOrderByNumberAsync(string orderNumber, string userId, bool isAdmin = false);
        Task<Result<int?>> GetOrderCountByCustomerAsync(string userId);
        Task<Result<decimal>> GetTotalRevenueByCustomerAsync(string userId);
        Task<Result<int?>> GetTotalOrderCountAsync(OrderStatus? status);
    }
}