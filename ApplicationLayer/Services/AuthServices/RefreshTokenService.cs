using Application.DtoModels;
using Application.DtoModels.TokenDtos;
using Application.Interfaces;
using Application.Services;
using Application.Services.EmailServices;
using Domain.Models;
using Hangfire;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Application.Services.AuthServices
{

    public class RefreshTokenService : IRefreshTokenService
    {
        private readonly IConnectionMultiplexer _redis;
        private readonly ILogger<RefreshTokenService> _logger;
        private readonly IConfiguration _config;
        private readonly UserManager<Customer> _userManager;
        private readonly IBackgroundJobClient _backgroundJobClient;
        private readonly IDatabase _database;

        private readonly TimeSpan _refreshTokenExpiry;

        public RefreshTokenService(
            IBackgroundJobClient backgroundJobClient,
            ILogger<RefreshTokenService> logger,
            IConnectionMultiplexer redis,
            IConfiguration config,
            UserManager<Customer> userManager)
        {
            _backgroundJobClient = backgroundJobClient;
            _logger = logger;
            _userManager = userManager;
            _redis = redis;
            _database = _redis.GetDatabase();
            _config = config;

            // Single source of truth for refresh token expiration
            int expiryHours = _config.GetValue<int>("JwtSettings:RefreshTokenExpiryHours", 168); // 7 days default
            _refreshTokenExpiry = TimeSpan.FromHours(expiryHours);
        }

        private static string GenerateRedisKey(string tokenHash) => $"RefreshToken:{tokenHash}";

        private static string GenerateUserTokenSetKey(string userId) => $"UserRefreshTokens:{userId}";

        private static string HashToken(string token)
        {
            using var sha256 = SHA256.Create();
            var bytes = Encoding.UTF8.GetBytes(token);
            var hash = sha256.ComputeHash(bytes);
            return Convert.ToBase64String(hash);
        }

        public async Task<Result<RefreshTokenResponse>> RefreshTokenAsync(string refreshToken)
        {
            _logger.LogInformation("RefreshTokenAsync started");

        
            string tokenHash = HashToken(refreshToken);

            
            string? value = await _database.StringGetAsync(GenerateRedisKey(tokenHash));

            if (string.IsNullOrEmpty(value))
            {
                _logger.LogWarning("Refresh token not found in Redis");
                return Result<RefreshTokenResponse>.Fail("Invalid or expired refresh token");
            }

            
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

            // Hash the incoming token for lookup
            string tokenHash = HashToken(refreshToken);

            // Check if token exists in Redis
            string? value = await _database.StringGetAsync(GenerateRedisKey(tokenHash));

            if (string.IsNullOrEmpty(value))
            {
                _logger.LogWarning("Refresh token not found in Redis");
                return Result<(string, string)>.Fail("Invalid or expired refresh token");
            }

            // Deserialize with null check
            RefreshTokenData? data;
            try
            {
                data = JsonSerializer.Deserialize<RefreshTokenData>(value);
                if (data == null)
                {
                    _logger.LogError("Failed to deserialize refresh token data");
                    return Result<(string, string)>.Fail("Invalid token data");
                }
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, "Error deserializing refresh token data");
                return Result<(string, string)>.Fail("Invalid token format");
            }

            // Rotate refresh token: generate new, store new, then delete old
            var rotationResult = await RotateRefreshTokenAsync(refreshToken, data.UserId);
            if (!rotationResult.Success || string.IsNullOrEmpty(rotationResult.Data))
            {
                _logger.LogError("Failed to rotate refresh token");
                return Result<(string, string)>.Fail("Unable to rotate refresh token");
            }

            // Return both the user ID and the new refresh token
            return Result<(string, string)>.Ok((data.UserId, rotationResult.Data), "Token refreshed successfully");
        }

        public async Task<Result<string>> GenerateRefreshTokenAsync(string userId)
        {
            _logger.LogInformation("Generating Refresh Token for User ID: {UserId}", userId);

          
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

            var value = JsonSerializer.Serialize(tokenData);

            
            string tokenHash = HashToken(token);

          
            bool stored = await _database.StringSetAsync(
                GenerateRedisKey(tokenHash),
                value,
                _refreshTokenExpiry,
                When.Always
            );

            if (!stored)
            {
                _logger.LogError("Failed to store refresh token in Redis for User ID: {UserId}", userId);
                return Result<string>.Fail("Could not store refresh token");
            }

         
            await _database.SetAddAsync(GenerateUserTokenSetKey(userId), tokenHash);

            _logger.LogInformation("RefreshToken generated and stored for User ID: {UserId}", userId);
            return Result<string>.Ok(token, "RefreshToken Generated");
        }

        public async Task<Result<bool>> RemoveRefreshTokenAsync(string token)
        {
            try
            {
               
                string tokenHash = HashToken(token);
                string key = GenerateRedisKey(tokenHash);
                
               
                string? value = await _database.StringGetAsync(key);
                string userId = string.Empty;
                
                if (!string.IsNullOrEmpty(value))
                {
                    try
                    {
                        var data = JsonSerializer.Deserialize<RefreshTokenData>(value);
                        if (data != null)
                        {
                            userId = data.UserId;
                        }
                    }
                    catch (JsonException)
                    {
                        // If we can't deserialize, we'll still delete the token
                    }
                }
                
                bool deleted = await _database.KeyDeleteAsync(key);

                if (!deleted)
                {
                    _logger.LogWarning("RefreshToken not found or already deleted: {Token}", token);
                   
                    return Result<bool>.Ok(true, "Token already removed or expired");
                }

           
                if (!string.IsNullOrEmpty(userId))
                {
                    await _database.SetRemoveAsync(GenerateUserTokenSetKey(userId), tokenHash);
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
            string? value = await _database.StringGetAsync(GenerateRedisKey(tokenHash));

            if (string.IsNullOrEmpty(value))
            {
                _logger.LogWarning("Refresh token not found in Redis");
                return Result<string>.Fail("Invalid or expired refresh token");
            }

            try
            {
                var data = JsonSerializer.Deserialize<RefreshTokenData>(value);
                if (data == null)
                {
                    _logger.LogError("Failed to deserialize refresh token data");
                    return Result<string>.Fail("Invalid token data");
                }

                return Result<string>.Ok(data.UserId);
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, "Error deserializing refresh token data");
                return Result<string>.Fail("Invalid token format");
            }
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
                var tokenHashes = await _database.SetMembersAsync(GenerateUserTokenSetKey(userId));

                if (tokenHashes.Length == 0)
                {
                    _logger.LogInformation("No refresh tokens found for User ID: {UserId}", userId);
                    return Result<bool>.Ok(true, "No tokens to remove");
                }

                
                var keysToDelete = new List<RedisKey>();
                foreach (var tokenHash in tokenHashes)
                {
                    keysToDelete.Add(GenerateRedisKey(tokenHash));
                }

                if (keysToDelete.Count > 0)
                {
                    await _database.KeyDeleteAsync(keysToDelete.ToArray());
                }

               
                await _database.KeyDeleteAsync(userTokenSetKey);

                _logger.LogInformation("Successfully removed {Count} refresh tokens for User ID: {UserId}", tokenHashes.Length, userId);
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