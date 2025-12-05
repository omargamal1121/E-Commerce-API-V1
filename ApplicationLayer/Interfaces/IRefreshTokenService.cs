using ApplicationLayer.Services;
using ApplicationLayer.Services.AuthServices;

namespace ApplicationLayer.Interfaces
{
	public interface IRefreshTokenService 
	{
	
		public Task<Result<bool>> ValidateRefreshTokenAsync(string userid, string securitystamp);
		public Task<Result<bool>> RemoveRefreshTokenAsync(string refreshtoken);
		public Task<Result<string>> GenerateRefreshTokenAsync(string userid,string securitystamp);
		public Task<Result<RefreshTokenResponse>> RefreshTokenAsync(string refreshtoken);
		

    }
}


