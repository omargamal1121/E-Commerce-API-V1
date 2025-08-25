using E_Commerce.DtoModels.Responses;
using E_Commerce.Services;
using System.Threading.Tasks;

namespace E_Commerce.Interfaces
{
    public interface ICustomerOrderService
    {
        Task<Result<bool>> CancelOrderByCustomerAsync(int orderId, string userId);
    }
}