using E_Commerce.DtoModels.OrderDtos;
using E_Commerce.Enums;

namespace E_Commerce.Services.Order
{
    public interface IOrderCommandService
    {
        Task<Result<int>> CountOrdersAsync(
      OrderStatus?status = null,
     bool? isDelete = null,
     bool isAdmin = false)
        ;
            Task<Result<OrderAfterCreatedto>> CreateOrderFromCartAsync(string userId, CreateOrderDto orderDto);
        Task<Result<bool>> UpdateOrderAfterPaid(int orderId, OrderStatus orderStatus);
        Task<Result<bool>> ConfirmOrderAsync(int orderId, string adminId, bool IsSysyem = false, bool IsAdmin = false, string? notes = null);
        Task<Result<bool>> ProcessOrderAsync(int orderId, string adminId,  string? notes = null);
        Task<Result<bool>> RefundOrderAsync(int orderId, string adminId, string? notes = null);
        Task<Result<bool>> ReturnOrderAsync(int orderId, string adminId, string? notes = null);
        Task<Result<bool>> ExpirePaymentAsync(int orderId, string adminId, bool IsSysyem = false, bool IsAdmin = false, string? notes = null);
        Task<Result<bool>> CompleteOrderAsync(int orderId, string adminId, string? notes = null);
        Task<Result<bool>> ShipOrderAsync(int orderId, string adminId, string? notes = null);
        Task<Result<bool>> DeliverOrderAsync(int orderId, string adminId, string? notes = null);
        Task<Result<bool>> ShipOrderAsync(int orderId, string userId);
        Task<Result<bool>> DeliverOrderAsync(int orderId, string userId);
        Task<Result<bool>> CancelOrderByCustomerAsync(int orderId, string userId);
        Task<Result<bool>> CancelOrderByAdminAsync(int orderId, string adminId);
        Task ExpireUnpaidOrderInBackground(int orderId);
        Task RestockOrderItemsInBackground(int orderId);
    }
}
