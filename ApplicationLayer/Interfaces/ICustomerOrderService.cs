using ApplicationLayer.DtoModels.Responses;
using ApplicationLayer.Services;
using System.Threading.Tasks;

namespace ApplicationLayer.Interfaces
{
    public interface ICustomerOrderService
    {
        Task<Result<bool>> CancelOrderByCustomerAsync(int orderId, string userId);
    }
}

