using E_Commerce.DtoModels.Responses;

namespace E_Commerce.Services.AccountServices.Password
{
    public interface IPasswordService
    {
        Task<Result<bool>> ChangePasswordAsync(string userid, string oldPassword, string newPassword);
        Task<Result<bool>> RequestPasswordResetAsync(string email);
        Task<Result<bool>> ResetPasswordAsync(string email, string token, string newPassword);
    }
} 