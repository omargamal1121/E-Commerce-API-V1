using Domain.Enums;
using Application.DtoModels.OrderDtos;
using Application.DtoModels.Responses;
using Application.Services;
using System.Threading.Tasks;

namespace Application.Interfaces
{
    public interface IOrderCreationService
    {
        Task<Result<OrderWithPaymentDto>> CreateOrderFromCartAsync(string userId, CreateOrderDto orderDto);
        Task<Result<bool>> UpdateOrderAfterPaid(int orderId, OrderStatus orderStatus);
        Task<Result<bool>> ExpirePaymentAsync(int orderId, string adminId, string? notes = null);
    }
}

