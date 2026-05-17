using Application.DtoModels.AccountDtos;
using Application.DtoModels.Responses;

namespace Application.Services.AccountServices.Registration
{
    public interface IRegistrationService
    {
        Task<Result<RegisterResponse>> RegisterAsync(RegisterDto model);
        Task<Result<bool>> ConfirmEmailAsync(string userId, string token);
        Task<Result<bool>> ResendConfirmationEmailAsync(string email);
    }
} 

