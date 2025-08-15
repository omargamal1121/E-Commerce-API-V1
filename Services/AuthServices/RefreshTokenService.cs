using E_Commerce.Interfaces;
using E_Commerce.Models;
using E_Commerce.Services.EmailServices;
using Hangfire;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using StackExchange.Redis;
using System.Security.Cryptography;

namespace E_Commerce.Services
{
	public class RefreshTokenService : IRefreshTokenService
	{
		private readonly IConnectionMultiplexer _redis;
		private readonly ILogger<TokenService> _logger;
		private readonly IConfiguration _config;
		private readonly UserManager<Customer> _userManager;
		private readonly IBackgroundJobClient _backgroundJobClient;
		private readonly IDatabase _database;
		private readonly ITokenService _tokenHelper;

		private readonly TimeSpan _refreshTokenExpiry;

		public RefreshTokenService(
			IBackgroundJobClient backgroundJobClient,
			ITokenService tokenHelper,
			ILogger<TokenService> logger,
			IConnectionMultiplexer redis,
			IConfiguration config,
			UserManager<Customer> userManager)
		{
			_backgroundJobClient = backgroundJobClient;
			_tokenHelper = tokenHelper;
			_logger = logger;
			_userManager = userManager;
			_redis = redis;
			_database = _redis.GetDatabase();
			_config = config;

			int expiryHours = _config.GetValue<int>("JwtSettings:RefreshTokenExpiryHours", 4);
			_refreshTokenExpiry = TimeSpan.FromHours(expiryHours);
		}

		private static string GenerateRedisKey(string token) => $"RefreshToken:{token}";

		public async Task<Result<string>> RefreshTokenAsync(string refreshToken)
		{
			_logger.LogInformation("🔄 RefreshTokenAsync started with token: {Token}", refreshToken);

			var isValid = await ValidateRefreshTokenAsync(refreshToken);
			if (!isValid.Success)
				return Result<string>.Fail(isValid.Message);

			string? userId = await _database.StringGetAsync(GenerateRedisKey(refreshToken));
			if (string.IsNullOrEmpty(userId))
				return Result<string>.Fail("User not found for the given refresh token.");

			_ = await RemoveRefreshTokenAsync(refreshToken);

			return await _tokenHelper.GenerateTokenAsync(userId);
		}

		public async Task<Result<string>> GenerateRefreshTokenAsync(string userId)
		{
			_logger.LogInformation("🔑 Generating Refresh Token for User ID: {UserId}", userId);

			var tokenBytes = new byte[64];
			using (var rng = RandomNumberGenerator.Create())
			{
				rng.GetBytes(tokenBytes);
			}
			string token = Convert.ToBase64String(tokenBytes)
				.Replace("+", "-")
				.Replace("/", "_")
				.Replace("=", "");

			bool stored = await _database.StringSetAsync(
				GenerateRedisKey(token),
				userId,
				_refreshTokenExpiry,
				When.Always
			);

			if (!stored)
			{
				_logger.LogError("❌ Failed to store refresh token in Redis for User ID: {UserId}", userId);
				return Result<string>.Fail("Could not store refresh token");
			}

			_logger.LogInformation("✅ RefreshToken generated and stored for User ID: {UserId}", userId);
			return Result<string>.Ok(token, "RefreshToken Generated");
		}


		public async Task<Result<bool>> RemoveRefreshTokenAsync(string token)
		{
			string key = GenerateRedisKey(token);
			bool deleted = await _database.KeyDeleteAsync(key);

			if (!deleted)
			{
				_logger.LogWarning("⚠️ Failed to remove RefreshToken for token: {Token}", token);
				_backgroundJobClient.Enqueue<IErrorNotificationService>(e =>
					e.SendErrorNotificationAsync("Can't Delete Refresh token", "Services/auth/refresh token/remove"));
				return Result<bool>.Fail($"❌ Failed to remove RefreshToken");
			}

			_logger.LogInformation("🗑️ Successfully removed RefreshToken for token: {Token}", token);
			return Result<bool>.Ok(true);
		}

		public async Task<Result<bool>> ValidateRefreshTokenAsync(string refreshToken)
		{
			_logger.LogInformation("🔍 Validating refresh token: {Token}", refreshToken);

			string? userIdStored = await _database.StringGetAsync(GenerateRedisKey(refreshToken));
			if (string.IsNullOrEmpty(userIdStored))
			{
				_logger.LogWarning("❌ Refresh token is invalid or expired");
				return Result<bool>.Fail("Refreshtoken Invalid Or Doesn't Exist");
			}

			bool exists = await _userManager.Users.AnyAsync(u => u.Id == userIdStored);
			if (!exists)
			{
				_logger.LogWarning("❌ User ID linked to refresh token not found: {UserId}", userIdStored);
				return Result<bool>.Fail("Refreshtoken Invalid Or Doesn't Exist");
			}

			_logger.LogInformation("✅ Refresh token is valid for User ID: {UserId}", userIdStored);
			return Result<bool>.Ok(true, "Valid Refreshtoken");
		}
	}
}
