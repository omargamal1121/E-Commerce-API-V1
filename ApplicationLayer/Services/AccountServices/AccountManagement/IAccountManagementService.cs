using Application.DtoModels.Responses;
using Application.Services;

namespace Application.Services.AccountServices.AccountManagement
{
    public interface IAccountManagementService
    {
        Task<Result<bool>> DeleteAsync(string id);
    }
} 

