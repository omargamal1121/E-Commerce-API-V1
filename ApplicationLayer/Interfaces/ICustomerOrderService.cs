using Application.DtoModels.Responses;
using Application.Services;
using System.Threading.Tasks;

namespace Application.Interfaces
{
    public interface ICustomerOrderService
    {
        Task<Result<bool>> CancelOrderByCustomerAsync(int orderId, string userId);
    }
}

