using ApplicationLayer.DtoModels;
using ApplicationLayer.Interfaces;
using ApplicationLayer.Services;
using ApplicationLayer.Services.EmailServices;
using DomainLayer.Models;
using Hangfire;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;
using System.Security.Cryptography;
using System.Text.Json;

namespace ApplicationLayer.Services.AuthServices
{

    public class RefreshTokenService : IRefreshTokenService
    {
        private readonly IConnectionMultiplexer _redis;
        private readonly ILogger<RefreshTokenService> _logger;
        private readonly IConfiguration _config;
        private readonly UserManager<Customer> _userManager;
        private readonly IBackgroundJobClient _backgroundJobClient;
        private readonly IDatabase _database;
        private readonly ITokenService _tokenHelper;

        private readonly TimeSpan _refreshTokenExpiry;

        public RefreshTokenService(
            IBackgroundJobClient backgroundJobClient,
            ITokenService tokenHelper,
            ILogger<RefreshTokenService> logger,
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

            int expiryHours = _config.GetValue<int>("JwtSettings:RefreshTokenExpiryHours", 168); // 7 days default
            _refreshTokenExpiry = TimeSpan.FromHours(expiryHours);
        }

        private static string GenerateRedisKey(string token) => $"RefreshToken:{token}";

        public async Task<Result<RefreshTokenResponse>> RefreshTokenAsync(string refreshToken)
        {
            _logger.LogInformation("RefreshTokenAsync started");

            // Check if token exists in Redis
            string? value = await _database.StringGetAsync(GenerateRedisKey(refreshToken));

            if (string.IsNullOrEmpty(value))
            {
                _logger.LogWarning("Refresh token not found in Redis");
                return Result<RefreshTokenResponse>.Fail("Invalid or expired refresh token");
            }

            // Deserialize with null check
            RefreshTokenData? data;
            try
            {
                data = JsonSerializer.Deserialize<RefreshTokenData>(value);
                if (data == null)
                {
                    _logger.LogError("Failed to deserialize refresh token data");
                    return Result<RefreshTokenResponse>.Fail("Invalid token data");
                }
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, "Error deserializing refresh token data");
                return Result<RefreshTokenResponse>.Fail("Invalid token format");
            }

            // Validate user and security stamp
            var userValidation = await ValidateRefreshTokenAsync(data.UserId, data.SecurityStamp);
            if (!userValidation.Success)
            {
                await RemoveRefreshTokenAsync(refreshToken); // Clean up invalid token
                return Result<RefreshTokenResponse>.Fail(userValidation.Message);
            }

            // Generate new refresh token
            var newRefresh = await GenerateRefreshTokenAsync(data.UserId, data.SecurityStamp);
            if (!newRefresh.Success || string.IsNullOrEmpty(newRefresh.Data))
            {
                _logger.LogError("Failed to generate new refresh token");
                return Result<RefreshTokenResponse>.Fail("Unable to generate new refresh token");
            }

            // Remove old refresh token
            await RemoveRefreshTokenAsync(refreshToken);

            // Get user for token generation
            var user = await _userManager.FindByIdAsync(data.UserId);
            if (user == null)
            {
                _logger.LogError("User not found after validation: {UserId}", data.UserId);
                return Result<RefreshTokenResponse>.Fail("User not found");
            }

            // Generate new access token
            var tokenResult = await _tokenHelper.GenerateTokenAsync(user);
            if (!tokenResult.Success || string.IsNullOrEmpty(tokenResult.Data))
            {
                _logger.LogError("Failed to generate access token");
                return Result<RefreshTokenResponse>.Fail("Unable to generate access token");
            }

            return Result<RefreshTokenResponse>.Ok(new RefreshTokenResponse
            {
                Token = tokenResult.Data,
                RefreshToken = newRefresh.Data
            });
        }

        public async Task<Result<string>> GenerateRefreshTokenAsync(string userId, string securityStamp)
        {
            _logger.LogInformation("Generating Refresh Token for User ID: {UserId}", userId);

            // Generate cryptographically secure random token
            var tokenBytes = new byte[64];
            using (var rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(tokenBytes);
            }

            string token = Convert.ToBase64String(tokenBytes)
                .Replace("+", "-")
                .Replace("/", "_")
                .Replace("=", "");

            // Create token data object
            var tokenData = new RefreshTokenData
            {
                UserId = userId,
                SecurityStamp = securityStamp
            };

            var value = JsonSerializer.Serialize(tokenData);

            // Store in Redis
            bool stored = await _database.StringSetAsync(
                GenerateRedisKey(token),
                value,
                _refreshTokenExpiry,
                When.Always
            );

            if (!stored)
            {
                _logger.LogError("Failed to store refresh token in Redis for User ID: {UserId}", userId);
                return Result<string>.Fail("Could not store refresh token");
            }

            _logger.LogInformation("RefreshToken generated and stored for User ID: {UserId}", userId);
            return Result<string>.Ok(token, "RefreshToken Generated");
        }

        public async Task<Result<bool>> RemoveRefreshTokenAsync(string token)
        {
            try
            {
                string key = GenerateRedisKey(token);
                bool deleted = await _database.KeyDeleteAsync(key);

                if (!deleted)
                {
                    _logger.LogWarning("RefreshToken not found or already deleted: {Token}", token);
                    // Not necessarily an error - token might have expired
                    return Result<bool>.Ok(true, "Token already removed or expired");
                }

                _logger.LogInformation("Successfully removed RefreshToken");
                return Result<bool>.Ok(true);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error removing refresh token");
                _backgroundJobClient.Enqueue<IErrorNotificationService>(e =>
                    e.SendErrorNotificationAsync($"Failed to delete refresh token: {ex.Message}",
                        "Services/auth/refresh token/remove"));
                return Result<bool>.Fail("Failed to remove RefreshToken");
            }
        }

        public async Task<Result<bool>> ValidateRefreshTokenAsync(string userId, string securityStamp)
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
            {
                _logger.LogWarning("Validation failed: User not found {UserId}", userId);
                return Result<bool>.Fail("User not found");
            }

            if (user.DeletedAt != null)
            {
                _logger.LogWarning("Validation failed: User account is deleted {UserId}", userId);
                return Result<bool>.Fail("User account is deleted");
            }

            if (user.SecurityStamp != securityStamp)
            {
                _logger.LogWarning("Validation failed: Security stamp mismatch for {UserId}", userId);
                return Result<bool>.Fail("Security stamp mismatch");
            }

            return Result<bool>.Ok(true);
        }
    }
}