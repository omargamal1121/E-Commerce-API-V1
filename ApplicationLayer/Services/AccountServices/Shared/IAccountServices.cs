using ApplicationLayer.DtoModels;
using ApplicationLayer.DtoModels.AccountDtos;
using ApplicationLayer.DtoModels.Responses;
using ApplicationLayer.DtoModels.TokenDtos;
using DomainLayer.Models;
using ApplicationLayer.Services;
using Microsoft.AspNetCore.Http;

namespace ApplicationLayer.Services.AccountServices.Shared
{
	public interface IAccountServices
	{
		public  Task<Result<string>> RequestPasswordResetAsync(string email);
		public Task<Result<string>> ResetPasswordAsync(string email, string token, string newPassword);
		public Task<Result<TokensDto>> LoginAsync(string email, string password);
		public Task<Result<string>> RefreshTokenAsync();
		public Task<Result<string>> ChangePasswordAsync(string userid, string oldPassword, string newPassword);
		public Task<Result<ChangeEmailResultDto>> ChangeEmailAsync(string newEmail, string userid);
		public Task<Result<RegisterResponse>> RegisterAsync(RegisterDto model);
		public Task<Result<string>> LogoutAsync(string userid);
		public Task<Result<string>> DeleteAsync(string id);
		public Task<Result<UploadPhotoResponseDto>> UploadPhotoAsync(IFormFile image, string id);
		public Task<Result<string>> ConfirmEmailAsync(string userId, string token);
		public Task<Result<string>> ResendConfirmationEmailAsync(string email);
	}
}


