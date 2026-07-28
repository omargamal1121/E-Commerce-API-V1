using Application.DtoModels;
using Application.Services;
using Application.Services.AuthServices;

namespace Application.Interfaces
{
	public interface IRefreshTokenService
	{
		Task<Result<string>> ValidateRefreshTokenAsync(string refreshToken);
		Task<Result<bool>> RemoveRefreshTokenAsync(string refreshtoken);
		Task<Result<string>> GenerateRefreshTokenAsync(string userId);
		Task<Result<RefreshTokenResponse>> RefreshTokenAsync(string refreshtoken);
		Task<Result<string>> RotateRefreshTokenAsync(string oldRefreshToken, string userId);
		Task<Result<(string UserId, string NewRefreshToken)>> RefreshTokenWithUserAsync(string refreshToken);
		Task<Result<bool>> RemoveAllRefreshTokensAsync(string userId);
	}
}


