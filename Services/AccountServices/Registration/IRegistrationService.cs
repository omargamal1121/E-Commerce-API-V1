using E_Commerce.DtoModels.AccountDtos;
using E_Commerce.DtoModels.Responses;

namespace E_Commerce.Services.AccountServices.Registration
{
    public interface IRegistrationService
    {
        Task<Result<RegisterResponse>> RegisterAsync(RegisterDto model);
        Task<Result<bool>> ConfirmEmailAsync(string userId, string token);
        Task<Result<bool>> ResendConfirmationEmailAsync(string email);
    }
} 