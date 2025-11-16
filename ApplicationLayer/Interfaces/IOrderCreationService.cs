using DomainLayer.Enums;
using ApplicationLayer.DtoModels.OrderDtos;
using ApplicationLayer.DtoModels.Responses;
using ApplicationLayer.Services;
using System.Threading.Tasks;

namespace ApplicationLayer.Interfaces
{
    public interface IOrderCreationService
    {
        Task<Result<OrderWithPaymentDto>> CreateOrderFromCartAsync(string userId, CreateOrderDto orderDto);
        Task<Result<bool>> UpdateOrderAfterPaid(int orderId, OrderStatus orderStatus);
        Task<Result<bool>> ExpirePaymentAsync(int orderId, string adminId, string? notes = null);
    }
}

