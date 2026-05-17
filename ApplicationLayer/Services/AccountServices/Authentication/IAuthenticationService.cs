using Application.DtoModels.Responses;
using Application.DtoModels.TokenDtos;
using Application.Services;

namespace Application.Services.AccountServices.Authentication
{
    public interface IAuthenticationService
    {
        Task<Result<TokensDto>> LoginAsync(string email, string password);
        Task<Result<bool>> LogoutAsync(string userid);
        Task<Result<TokensDto>> RefreshTokenAsync();
    }
} 

