using E_Commerce.DtoModels.OrderDtos;
using E_Commerce.DtoModels.Responses;
using E_Commerce.Enums;
using E_Commerce.Services;
using System.Threading.Tasks;

namespace E_Commerce.Interfaces
{
    public interface IOrderCreationService
    {
        Task<Result<OrderWithPaymentDto>> CreateOrderFromCartAsync(string userId, CreateOrderDto orderDto);
        Task<Result<bool>> UpdateOrderAfterPaid(int orderId, OrderStatus orderStatus);
        Task<Result<bool>> ExpirePaymentAsync(int orderId, string adminId, string? notes = null);
    }
}