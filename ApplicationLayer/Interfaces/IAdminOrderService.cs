
using Application.DtoModels.OrderDtos;
using Application.Services;
using Domain.Enums;

namespace Application.Interfaces
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

