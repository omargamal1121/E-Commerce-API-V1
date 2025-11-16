using ApplicationLayer.DtoModels.Responses;
using ApplicationLayer.Services;

namespace ApplicationLayer.Services.AccountServices.AccountManagement
{
    public interface IAccountManagementService
    {
        Task<Result<bool>> DeleteAsync(string id);
    }
} 

