using E_Commerce.DtoModels.OrderDtos;
using E_Commerce.Enums;
using E_Commerce.Services;

namespace E_Commerce.Interfaces
{
    public interface IAdminOrderService
    {
        Task<Result<OrderDto>> UpdateOrderStatusAsync(int orderId, OrderStatus newStatus);
        Task<Result<bool>> ProcessOrderAsync(int orderId);
        Task<Result<bool>> RefundOrderAsync(int orderId);
        Task<Result<bool>> ConfirmOrderAsync(int orderId);
        Task<Result<bool>> ShipOrderAsync(int orderId);
        Task<Result<bool>> DeliverOrderAsync(int orderId);
        Task<Result<bool>> CancelOrderByAdminAsync(int orderId);
        Task<Result<bool>> ExpireOrderAsync(int orderId);
        Task<Result<bool>> CompleteOrderAsync(int orderId);
        Task<Result<bool>> ReturnOrderAsync(int orderId);
        Task<Result<int>> GetOrdersCountAsync();
        Task<Result<decimal>> GetTotalRevenueAsync();
    }
}