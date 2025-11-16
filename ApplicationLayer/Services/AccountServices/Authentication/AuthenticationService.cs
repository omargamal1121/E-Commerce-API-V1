using ApplicationLayer.DtoModels.Responses;
using ApplicationLayer.DtoModels.TokenDtos;
using ApplicationLayer.Interfaces;

using ApplicationLayer.Services;
using ApplicationLayer.Services.EmailServices;
using DomainLayer.Models;
using Hangfire;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace ApplicationLayer.Services.AccountServices.Authentication
{
	public class AuthenticationService : IAuthenticationService
	{
		private readonly ILogger<AuthenticationService> _logger;
		private readonly UserManager<Customer> _userManager;
		private readonly IRefreshTokenService _refreshTokenService;
		private readonly ITokenService _tokenService;
		private readonly IErrorNotificationService _errorNotificationService;
		private readonly IAccountEmailService _accountEmailService;
		private readonly IConfiguration _configuration;
		private readonly IHttpContextAccessor _httpContextAccessor;

		private const string RefreshCookieName = "Refresh";

		public AuthenticationService(
			IHttpContextAccessor httpContextAccessor,
			ILogger<AuthenticationService> logger,
			UserManager<Customer> userManager,
			IRefreshTokenService refreshTokenService,
			ITokenService tokenService,
			IErrorNotificationService errorNotificationService,
			IAccountEmailService accountEmailService,
			IConfiguration configuration)
		{
			_httpContextAccessor = httpContextAccessor;
			_logger = logger;
			_userManager = userManager;
			_refreshTokenService = refreshTokenService;
			_tokenService = tokenService;
			_errorNotificationService = errorNotificationService;
			_accountEmailService = accountEmailService;
			_configuration = configuration;
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

				await EnsureLockoutEnabled(user);

				if (await _userManager.IsLockedOutAsync(user))
					return Result<TokensDto>.Fail("Your account is currently locked. Please try again later.", 403);

				if (!await _userManager.CheckPasswordAsync(user, password))
					return await HandleFailedLoginAttemptAsync(user);

				await _userManager.ResetAccessFailedCountAsync(user);

				if(user.DeletedAt!=null)
				{
					_logger.LogInformation("Login failed: Account deleted for {Email}", email);
                    return Result<TokensDto>.Fail("Invalid email or password.", 400);
                }
                var tokenResult = await _tokenService.GenerateTokenAsync(user);
				if (!tokenResult.Success || tokenResult.Data == null)
				{
					_logger.LogError("Failed to generate token: {Message}", tokenResult.Message);
					return Result<TokensDto>.Fail("An error occurred during login.", 500);
				}
			

				var refreshTokenResult = await _refreshTokenService.GenerateRefreshTokenAsync(user.Id);
				if (refreshTokenResult.Success && refreshTokenResult.Data != null)
					SetRefreshCookie(refreshTokenResult.Data);
				else
					_logger.LogError("Failed to generate refresh token: {Message}", refreshTokenResult.Message);
				var roles = (await _userManager.GetRolesAsync(user)).ToList();
                return Result<TokensDto>.Ok(new TokensDto { Token = tokenResult.Data , Roles=roles }, "Login successfully", 200);
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

			var refreshToken = GetRefreshCookie();
			if (refreshToken != null)
				BackgroundJob.Enqueue<AuthenticationService>(s => s.RemoveRefreshTokenAsync(refreshToken));

			var customer = await _userManager.FindByIdAsync(userId);
			if (customer == null)
			{
				_logger.LogError("No user found with ID: {UserId}", userId);
				return Result<bool>.Fail("Invalid user ID", 401);
			}

			var updateResult = await _userManager.UpdateSecurityStampAsync(customer);
			if (!updateResult.Succeeded)
			{
				var errors = string.Join(", ", updateResult.Errors.Select(e => e.Description));
				BackgroundJob.Enqueue<IErrorNotificationService>(e =>
					e.SendErrorNotificationAsync(errors, $"{nameof(AuthenticationService)}/{nameof(LogoutAsync)}"));
			}

			return Result<bool>.Ok(true, "Logout Successful", 200);
		}

		public async Task<Result<TokensDto>> RefreshTokenAsync()
		{
			var refreshToken = GetRefreshCookie();
			if (string.IsNullOrEmpty(refreshToken))
			{
				_logger.LogWarning("Refresh token not found in cookies.");
				return Result<TokensDto>.Fail("Please login again", 401);
			}

			var tokenResult = await _refreshTokenService.RefreshTokenAsync(refreshToken);
			if (!tokenResult.Success || tokenResult.Data == null)
			{
				_logger.LogWarning("Failed to refresh token. Removing refresh token.");
				await RemoveRefreshTokenAsync(refreshToken);
				ExpireRefreshCookie();
				return Result<TokensDto>.Fail("Failed to generate token. Please login again.", 401);
			}
			var token = new TokensDto
			{
				
				Token = tokenResult.Data
			};
			return Result<TokensDto>.Ok(token, "Token generated", 200);
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

		private string? GetRefreshCookie() =>
			_httpContextAccessor?.HttpContext?.Request.Cookies[RefreshCookieName];

		private void SetRefreshCookie(string token)
		{
			_httpContextAccessor?.HttpContext?.Response.Cookies.Append(
				RefreshCookieName,
				token,
				new CookieOptions
				{
					HttpOnly = true,
					Secure = true,
					SameSite = SameSiteMode.Strict,
					Expires = DateTimeOffset.UtcNow.AddDays(7)
				});
		}

		private void ExpireRefreshCookie()
		{
			_httpContextAccessor?.HttpContext?.Response.Cookies.Append(
				RefreshCookieName,
				string.Empty,
				new CookieOptions
				{
					Expires = DateTimeOffset.UtcNow.AddDays(-1),
					HttpOnly = true,
					Secure = true,
					SameSite = SameSiteMode.Strict
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


