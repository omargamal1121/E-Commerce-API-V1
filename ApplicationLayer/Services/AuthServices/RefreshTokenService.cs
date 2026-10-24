using Application.DtoModels;
using Application.DtoModels.TokenDtos;
using Application.Interfaces;
using Application.Services;
using Application.Services.CacheServices;
using Application.Services.EmailServices;
using Domain.Models;
using Hangfire;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Application.Services.AuthServices
{
    public class RefreshTokenService : IRefreshTokenService
    {
        private readonly ILogger<RefreshTokenService> _logger;
        private readonly IConfiguration _config;
        private readonly UserManager<Customer> _userManager;
        private readonly IBackgroundJobClient _backgroundJobClient;
        private readonly ICacheManager _cacheManager;

        private readonly TimeSpan _refreshTokenExpiry;

        public RefreshTokenService(
            ICacheManager cacheManager,
            IBackgroundJobClient backgroundJobClient,
            ILogger<RefreshTokenService> logger,
            IConfiguration config,
            UserManager<Customer> userManager)
        {
            _backgroundJobClient = backgroundJobClient;
            _logger = logger;
            _userManager = userManager;
            _cacheManager = cacheManager;
            _config = config;

            // Single source of truth for refresh token expiration
            int expiryHours = _config.GetValue<int>("JwtSettings:RefreshTokenExpiryHours", 168); // 7 days default
            _refreshTokenExpiry = TimeSpan.FromHours(expiryHours);
        }

        private static string GenerateTokenKey(string tokenHash) => $"RefreshToken:{tokenHash}";

        private static string GenerateUserTokenSetKey(string userId) => $"UserRefreshTokens:{userId}";

        private static string GenerateUserTag(string userId) => $"user_tokens:{userId}";

        private static string HashToken(string token)
        {
            using var sha256 = SHA256.Create();
            var bytes = Encoding.UTF8.GetBytes(token);
            var hash = sha256.ComputeHash(bytes);
            return Convert.ToBase64String(hash);
        }

        private async Task<RefreshTokenData?> GetTokenDataAsync(string tokenHash)
        {
            string key = GenerateTokenKey(tokenHash);

            try
            {
                var data = await _cacheManager.GetAsync<RefreshTokenData>(key);
                if (data is not null)
                {
                    return data;
                }

                // Fallback in case token was cached as raw string
                var rawString = await _cacheManager.GetAsync<string>(key);
                if (!string.IsNullOrEmpty(rawString))
                {
                    return JsonSerializer.Deserialize<RefreshTokenData>(rawString);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving refresh token data for hash: {Hash}", tokenHash);
            }

            return null;
        }

        public async Task<Result<RefreshTokenResponse>> RefreshTokenAsync(string refreshToken)
        {
            _logger.LogInformation("RefreshTokenAsync started");

            string tokenHash = HashToken(refreshToken);
            var data = await GetTokenDataAsync(tokenHash);

            if (data == null)
            {
                _logger.LogWarning("Refresh token not found or expired");
                return Result<RefreshTokenResponse>.Fail("Invalid or expired refresh token");
            }

            if (data.ExpiresAt <= DateTime.UtcNow)
            {
                await RemoveRefreshTokenAsync(refreshToken);
                _logger.LogWarning("Refresh token expired for User ID: {UserId}", data.UserId);
                return Result<RefreshTokenResponse>.Fail("Invalid or expired refresh token");
            }

            var rotationResult = await RotateRefreshTokenAsync(refreshToken, data.UserId);
            if (!rotationResult.Success || string.IsNullOrEmpty(rotationResult.Data))
            {
                _logger.LogError("Failed to rotate refresh token");
                return Result<RefreshTokenResponse>.Fail("Unable to rotate refresh token");
            }

            // Return the new refresh token only (caller will generate access token)
            return Result<RefreshTokenResponse>.Ok(new RefreshTokenResponse
            {
                Token = null, // Access token generation moved to caller
                RefreshToken = rotationResult.Data
            });
        }

        /// <summary>
        /// Refreshes a token and returns the validated user ID along with the new refresh token.
        /// This eliminates duplicate user lookups in the caller.
        /// </summary>
        public async Task<Result<(string UserId, string NewRefreshToken)>> RefreshTokenWithUserAsync(string refreshToken)
        {
            _logger.LogInformation("RefreshTokenWithUserAsync started");

            string tokenHash = HashToken(refreshToken);
            var data = await GetTokenDataAsync(tokenHash);

            if (data == null)
            {
                _logger.LogWarning("Refresh token not found or expired");
                return Result<(string, string)>.Fail("Invalid or expired refresh token");
            }

            if (data.ExpiresAt <= DateTime.UtcNow)
            {
                await RemoveRefreshTokenAsync(refreshToken);
                _logger.LogWarning("Refresh token expired for User ID: {UserId}", data.UserId);
                return Result<(string, string)>.Fail("Invalid or expired refresh token");
            }

            var rotationResult = await RotateRefreshTokenAsync(refreshToken, data.UserId);
            if (!rotationResult.Success || string.IsNullOrEmpty(rotationResult.Data))
            {
                _logger.LogError("Failed to rotate refresh token");
                return Result<(string, string)>.Fail("Unable to rotate refresh token");
            }

            return Result<(string, string)>.Ok((data.UserId, rotationResult.Data), "Token refreshed successfully");
        }

        public async Task<Result<string>> GenerateRefreshTokenAsync(string userId)
        {
            _logger.LogInformation("Generating Refresh Token for User ID: {UserId}", userId);

            try
            {
                var tokenBytes = new byte[64];
                using (var rng = RandomNumberGenerator.Create())
                {
                    rng.GetBytes(tokenBytes);
                }

                string token = Convert.ToBase64String(tokenBytes)
                    .Replace("+", "-")
                    .Replace("/", "_")
                    .Replace("=", "");

                var now = DateTime.UtcNow;

                var tokenData = new RefreshTokenData
                {
                    UserId = userId,
                    CreatedAt = now,
                    ExpiresAt = now.Add(_refreshTokenExpiry)
                };

                string tokenHash = HashToken(token);
                string tokenKey = GenerateTokenKey(tokenHash);
                string userTag = GenerateUserTag(userId);

                // Store token in cache with user tag for bulk invalidation
                await _cacheManager.SetAsync(tokenKey, tokenData, _refreshTokenExpiry, new[] { userTag });

                // Also maintain user active token list
                string userSetKey = GenerateUserTokenSetKey(userId);
                var tokens = await _cacheManager.GetAsync<List<string>>(userSetKey) ?? new List<string>();
                if (!tokens.Contains(tokenHash))
                {
                    tokens.Add(tokenHash);
                    await _cacheManager.SetAsync(userSetKey, tokens, _refreshTokenExpiry);
                }

                _logger.LogInformation("RefreshToken generated and stored for User ID: {UserId}", userId);
                return Result<string>.Ok(token, "RefreshToken Generated");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to generate refresh token for User ID: {UserId}", userId);
                return Result<string>.Fail("Could not generate refresh token");
            }
        }

        public async Task<Result<bool>> RemoveRefreshTokenAsync(string token)
        {
            try
            {
                string tokenHash = HashToken(token);
                string key = GenerateTokenKey(tokenHash);

                var data = await GetTokenDataAsync(tokenHash);
                string userId = data?.UserId ?? string.Empty;

                await _cacheManager.RemoveAsync(key);

                if (!string.IsNullOrEmpty(userId))
                {
                    var userTokenSetKey = GenerateUserTokenSetKey(userId);
                    var tokens = await _cacheManager.GetAsync<List<string>>(userTokenSetKey);
                    if (tokens is not null && tokens.Remove(tokenHash))
                    {
                        if (tokens.Count > 0)
                        {
                            await _cacheManager.SetAsync(userTokenSetKey, tokens, _refreshTokenExpiry);
                        }
                        else
                        {
                            await _cacheManager.RemoveAsync(userTokenSetKey);
                        }
                    }
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

        public async Task<Result<string>> ValidateRefreshTokenAsync(string refreshToken)
        {
            string tokenHash = HashToken(refreshToken);
            var data = await GetTokenDataAsync(tokenHash);

            if (data == null)
            {
                _logger.LogWarning("Refresh token not found or expired");
                return Result<string>.Fail("Invalid or expired refresh token");
            }

            if (data.ExpiresAt <= DateTime.UtcNow)
            {
                await RemoveRefreshTokenAsync(refreshToken);
                _logger.LogWarning("Refresh token expired for User ID: {UserId}", data.UserId);
                return Result<string>.Fail("Refresh token has expired");
            }

            return Result<string>.Ok(data.UserId);
        }

        public async Task<Result<string>> RotateRefreshTokenAsync(string oldRefreshToken, string userId)
        {
            _logger.LogInformation("Rotating refresh token for User ID: {UserId}", userId);

            var newRefreshResult = await GenerateRefreshTokenAsync(userId);
            if (!newRefreshResult.Success || string.IsNullOrEmpty(newRefreshResult.Data))
            {
                _logger.LogError("Failed to generate new refresh token during rotation");
                return Result<string>.Fail("Unable to generate new refresh token");
            }

            await RemoveRefreshTokenAsync(oldRefreshToken);

            _logger.LogInformation("Refresh token rotated successfully for User ID: {UserId}", userId);
            return Result<string>.Ok(newRefreshResult.Data, "Refresh token rotated");
        }

        public async Task<Result<bool>> RemoveAllRefreshTokensAsync(string userId)
        {
            try
            {
                _logger.LogInformation("Removing all refresh tokens for User ID: {UserId}", userId);

                var userTokenSetKey = GenerateUserTokenSetKey(userId);
                var tokenHashes = await _cacheManager.GetAsync<List<string>>(userTokenSetKey);

                if (tokenHashes is not null && tokenHashes.Count > 0)
                {
                    foreach (var tokenHash in tokenHashes)
                    {
                        await _cacheManager.RemoveAsync(GenerateTokenKey(tokenHash));
                    }
                }

                // Invalidate by tag as well
                await _cacheManager.RemoveByTagAsync(GenerateUserTag(userId));
                await _cacheManager.RemoveAsync(userTokenSetKey);

                _logger.LogInformation("Successfully removed refresh tokens for User ID: {UserId}", userId);
                return Result<bool>.Ok(true, "All refresh tokens removed");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in RemoveAllRefreshTokensAsync");
                _backgroundJobClient.Enqueue<IErrorNotificationService>(e =>
                    e.SendErrorNotificationAsync($"Failed to remove refresh tokens for user: {ex.Message}",
                        "Services/auth/refresh token/remove-all"));
                return Result<bool>.Fail("Failed to remove refresh tokens");
            }
        }
    }
}