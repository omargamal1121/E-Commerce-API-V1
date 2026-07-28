using Application.DtoModels.Responses;
using Application.DtoModels.TokenDtos;
using Application.Interfaces;

using Application.Services;
using Application.Services.EmailServices;
using Domain.Models;
using Hangfire;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Application.Services.AccountServices.Authentication
{
	public class AuthenticationService : IAuthenticationService
	{
		private readonly ILogger<AuthenticationService> _logger;
		private readonly UserManager<Customer> _userManager;
		private readonly IRefreshTokenService _refreshTokenService;
		private readonly ITokenService _tokenService;
		private readonly IConfiguration _configuration;
		private readonly IRefreshTokenCookieService _refreshTokenCookieService;
		private readonly IBackgroundJobClient _backgroundJobClient;

		public AuthenticationService(
			IBackgroundJobClient backgroundJobClient,
			ILogger<AuthenticationService> logger,
			UserManager<Customer> userManager,
			IRefreshTokenService refreshTokenService,
			ITokenService tokenService,
			IConfiguration configuration,
			IRefreshTokenCookieService refreshTokenCookieService)
		{
			_backgroundJobClient = backgroundJobClient;
			_logger = logger;
			_userManager = userManager;
			_refreshTokenService = refreshTokenService;
			_tokenService = tokenService;
			_configuration = configuration;
			_refreshTokenCookieService = refreshTokenCookieService;
		}

		public async Task<Result<TokensDto>> LoginAsync(string email, string password)
		{
			try
			{
				var user = await _userManager.FindByEmailAsync(email);
				if (user == null)
				{
					_logger.LogWarning("Login failed: Email not found for {Email}", email);
					return Result<TokensDto>.Fail("Invalid email or password.", 400);
				}
				if (user.DeletedAt != null)
				{
					_logger.LogInformation("Login failed: Account deleted for {Email}", email);
					return Result<TokensDto>.Fail("Invalid email or password.", 400);
				}

				if (await _userManager.IsLockedOutAsync(user))
					return Result<TokensDto>.Fail("Your account is currently locked. Please try again later.", 403);

				if (!await _userManager.CheckPasswordAsync(user, password))
					return Result<TokensDto>.Fail("Invalid email or password.", 400);


				
				var tokensResult = await IssueTokensAsync(user);
				if (!tokensResult.Success || tokensResult.Data == null)
				{
					_logger.LogError("Failed to issue tokens: {Message}", tokensResult.Message);
					return Result<TokensDto>.Fail("An error occurred during login.", 500);
				}

				// Set refresh token in cookie
				if (!string.IsNullOrEmpty(tokensResult.Data.RefreshToken))
				{
					_refreshTokenCookieService.SetRefreshToken(tokensResult.Data.RefreshToken);
				}

				var roles = (await _userManager.GetRolesAsync(user)).ToList();
				return Result<TokensDto>.Ok(new TokensDto { Token = tokensResult.Data.AccessToken, RefreshToken = tokensResult.Data.RefreshToken, Roles = roles }, "Login successfully", 200);
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error occurred in LoginAsync.");
				return Result<TokensDto>.Fail("An error occurred during login.", 500);
			}
		}

		public async Task<Result<bool>> LogoutAsync(string userId)
		{
			_logger.LogInformation("Executing {Method}", nameof(LogoutAsync));

			var refreshToken = _refreshTokenCookieService.GetRefreshToken();
			if (refreshToken != null)
				BackgroundJob.Enqueue<AuthenticationService>(s => s.RemoveRefreshTokenAsync(refreshToken));

			var customer = await _userManager.FindByIdAsync(userId);
			if (customer == null)
			{
				_logger.LogError("No user found with ID: {UserId}", userId);
				return Result<bool>.Fail("Invalid user ID", 401);
			}

			// Invalidate all refresh tokens for this user
			await _refreshTokenService.RemoveAllRefreshTokensAsync(userId);

			_refreshTokenCookieService.RemoveRefreshToken();
			return Result<bool>.Ok(true, "Logout Successful", 200);
		}

		public async Task<Result<TokensDto>> RefreshTokenAsync(string refreshToken)
		{
			if (string.IsNullOrEmpty(refreshToken))
			{
				_logger.LogWarning("Refresh token not provided.");
				return Result<TokensDto>.Fail("Please login again", 401);
			}

			// Call RefreshTokenService to rotate the refresh token and get the validated user ID
			var refreshResult = await _refreshTokenService.RefreshTokenWithUserAsync(refreshToken);
			if (!refreshResult.Success)
			{
				_logger.LogWarning("Failed to refresh token. Removing refresh token.");
				await RemoveRefreshTokenAsync(refreshToken);
				_refreshTokenCookieService.RemoveRefreshToken();
				return Result<TokensDto>.Fail("Failed to generate token. Please login again.", 401);
			}

			var (userId, newRefreshToken) = refreshResult.Data;

			// Fetch the user from database
			var user = await _userManager.FindByIdAsync(userId);
			if (user == null || user.DeletedAt != null)
			{
				_logger.LogWarning("User not found or deleted during refresh: {UserId}", userId);
				await RemoveRefreshTokenAsync(refreshToken);
				_refreshTokenCookieService.RemoveRefreshToken();
				return Result<TokensDto>.Fail("User not found. Please login again.", 401);
			}

			// Generate new access token
			var accessTokenResult = await _tokenService.GenerateTokenAsync(user);
			if (!accessTokenResult.Success || string.IsNullOrEmpty(accessTokenResult.Data))
			{
				_logger.LogError("Failed to generate access token during refresh");
				_refreshTokenCookieService.RemoveRefreshToken();
				return Result<TokensDto>.Fail("Failed to generate token. Please login again.", 401);
			}

			// Set the new refresh token in cookie
			_refreshTokenCookieService.SetRefreshToken(newRefreshToken);

			var roles = (await _userManager.GetRolesAsync(user)).ToList();
			return Result<TokensDto>.Ok(new TokensDto { Token = accessTokenResult.Data, RefreshToken = newRefreshToken, Roles = roles }, "Token generated", 200);
		}

		public async Task RemoveRefreshTokenAsync(string refreshToken)
		{
			try
			{
				await _refreshTokenService.RemoveRefreshTokenAsync(refreshToken);
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error in {Method}", nameof(RemoveRefreshTokenAsync));
			}
		}

		#region Private Helpers

		private async Task<Result<TokenPair>> IssueTokensAsync(Customer user)
		{
			
			var accessTokenResult = await _tokenService.GenerateTokenAsync(user);
			if (!accessTokenResult.Success || string.IsNullOrEmpty(accessTokenResult.Data))
			{
				return Result<TokenPair>.Fail("Failed to generate access token");
			}

			
			var refreshTokenResult = await _refreshTokenService.GenerateRefreshTokenAsync(user.Id);
			if (!refreshTokenResult.Success || string.IsNullOrEmpty(refreshTokenResult.Data))
			{
				_logger.LogError("Failed to generate refresh token: {Message}", refreshTokenResult.Message);
				return Result<TokenPair>.Fail("Failed to generate refresh token");
			}

			return Result<TokenPair>.Ok(new TokenPair
			{
				AccessToken = accessTokenResult.Data,
				RefreshToken = refreshTokenResult.Data
			});
		}

		private async Task EnsureLockoutEnabled(Customer user)
		{
			if (!user.LockoutEnabled)
			{
				user.LockoutEnabled = true;
				await _userManager.UpdateAsync(user);
			}
		}

		private async Task<Result<TokensDto>> HandleFailedLoginAttemptAsync(Customer user)
		{
			await _userManager.AccessFailedAsync(user);
			var failedCount = await _userManager.GetAccessFailedCountAsync(user);
			var maxFailedAttempts = _configuration.GetValue("Security:LockoutPolicy:MaxFailedAttempts", 5);
			var lockoutDurationMinutes = _configuration.GetValue("Security:LockoutPolicy:LockoutDurationMinutes", 15);
			var permanentLockoutAfterAttempts = _configuration.GetValue("Security:LockoutPolicy:PermanentLockoutAfterAttempts", 10);

			if (failedCount >= permanentLockoutAfterAttempts)
			{
				user.LockoutEnd = DateTime.UtcNow.AddYears(100);
				await _userManager.UpdateAsync(user);
				BackgroundJob.Enqueue<IAccountEmailService>(e => e.SendAccountLockedEmailAsync(user.Email, user.UserName, $"Multiple failed login attempts ({permanentLockoutAfterAttempts}+ times)"));
				var resetToken = await _userManager.GeneratePasswordResetTokenAsync(user);
				var encodedToken = System.Net.WebUtility.UrlEncode(resetToken);
				BackgroundJob.Enqueue<IAccountEmailService>(e => e.SendPasswordResetEmailAsync(user.Email, user.UserName, encodedToken));
				return Result<TokensDto>.Fail("Your account has been permanently locked due to multiple failed login attempts. Please reset your password.", 403);
			}

			if (failedCount >= maxFailedAttempts)
			{
				user.LockoutEnd = DateTime.UtcNow.AddMinutes(lockoutDurationMinutes);
				await _userManager.UpdateAsync(user);
				BackgroundJob.Enqueue<IAccountEmailService>(e => e.SendAccountLockedEmailAsync(user.Email, user.UserName, $"Multiple failed login attempts ({maxFailedAttempts}+ times)"));
				return Result<TokensDto>.Fail($"Too many failed login attempts. Please try again after {lockoutDurationMinutes} minutes.", 403);
			}

			return Result<TokensDto>.Fail("Invalid email or password.", 401);
		}

		#endregion
	}
}


