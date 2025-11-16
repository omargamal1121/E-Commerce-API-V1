using ApplicationLayer.DtoModels.Responses;
using ApplicationLayer.DtoModels.TokenDtos;
using ApplicationLayer.Services;

namespace ApplicationLayer.Services.AccountServices.Authentication
{
    public interface IAuthenticationService
    {
        Task<Result<TokensDto>> LoginAsync(string email, string password);
        Task<Result<bool>> LogoutAsync(string userid);
        Task<Result<TokensDto>> RefreshTokenAsync();
    }
} 

