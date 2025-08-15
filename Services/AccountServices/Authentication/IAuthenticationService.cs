using E_Commerce.DtoModels.Responses;
using E_Commerce.DtoModels.TokenDtos;

namespace E_Commerce.Services.AccountServices.Authentication
{
    public interface IAuthenticationService
    {
        Task<Result<TokensDto>> LoginAsync(string email, string password);
        Task<Result<bool>> LogoutAsync(string userid);
        Task<Result<TokensDto>> RefreshTokenAsync();
    }
} 