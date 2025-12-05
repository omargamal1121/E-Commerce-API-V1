using ApplicationLayer.Interfaces;
using DomainLayer.Models;
using ApplicationLayer.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.IdentityModel.Tokens.Jwt;
using Microsoft.IdentityModel.Tokens;

namespace ApplicationLayer.Services.AuthServices
{
    public class TokenService : ITokenService
    {
        private readonly ILogger<TokenService> _logger;
        private readonly string _issuer;
        private readonly string _audience;
        private readonly string _secretkey;
        private readonly double _expiresInMinutes;
        private readonly IConfiguration _config;
        private readonly UserManager<Customer> _userManager;

        public TokenService(ILogger<TokenService> logger, IConfiguration config, UserManager<Customer> userManager)
        {
            _logger = logger;
            _userManager = userManager;
            _config = config;

            _secretkey = _config["Jwt:Key"]
                ?? throw new ArgumentNullException(nameof(_secretkey), "Jwt:Key is missing in appsettings.json");
            _issuer = _config["Jwt:Issuer"]
                ?? throw new ArgumentNullException(nameof(_issuer), "Jwt:Issuer is missing in appsettings.json");
            _audience = _config["Jwt:Audience"]
                ?? throw new ArgumentNullException(nameof(_audience), "Jwt:Audience is missing in appsettings.json");

            if (_secretkey.Length < 32)
            {
                throw new InvalidOperationException("JWT secret key must be at least 32 characters long");
            }

            if (!double.TryParse(_config["Jwt:ExpiresInMinutes"], out double expiresInMinutes))
            {
                _logger.LogWarning("JWT ExpiresInMinutes is missing, using default (15 minutes).");
                expiresInMinutes = 15;
            }
            _expiresInMinutes = expiresInMinutes;
        }

        public async Task<Result<string>> GenerateTokenAsync(Customer user)
        {
            _logger.LogInformation("Generating Access Token for User ID: {UserId}", user.Id);
            return await GenerateTokenInternalAsync(user);
        }

        public async Task<Result<string>> GenerateTokenAsync(string userId)
        {
            _logger.LogInformation("Generating Access Token for User ID: {UserId}", userId);

            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
            {
                _logger.LogWarning("User not found for ID: {UserId}", userId);
                return Result<string>.Fail("User not found");
            }

            return await GenerateTokenInternalAsync(user);
        }

        private async Task<Result<string>> GenerateTokenInternalAsync(Customer user)
        {
            _logger.LogInformation("Generating Access Token for User ID: {UserId}", user.Id);

            // Validate user account status
            if (user.DeletedAt != null)
            {
                _logger.LogWarning("Cannot generate token for deleted user: {UserId}", user.Id);
                return Result<string>.Fail("User account is deleted");
            }

            if (await _userManager.IsLockedOutAsync(user))
            {
                _logger.LogWarning("Cannot generate token for locked out user: {UserId}", user.Id);
                return Result<string>.Fail("User account is locked");
            }

            // Generate unique token identifier
            var jti = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));

            // Build base claims
            var claims = new List<Claim>
            {
                new(JwtRegisteredClaimNames.Jti, jti),
                new(ClaimTypes.NameIdentifier, user.Id),
                new(JwtRegisteredClaimNames.Iat, DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(), ClaimValueTypes.Integer64)
            };

            // Fetch roles and custom claims in parallel
            var roles = await _userManager.GetRolesAsync(user);
            var userclaims = await _userManager.GetClaimsAsync(user);
         

            // Add role claims
            foreach (var role in roles)
            {
                claims.Add(new Claim(ClaimTypes.Role, role));
            }

            // Add custom user claims
            foreach (var claim in userclaims)
            {
                claims.Add(claim);
            }

            // Create signing credentials
            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_secretkey));
            var signingCredentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            // NotBefore set to 30 seconds in the past to handle clock skew between servers
            var notBefore = DateTime.UtcNow.AddSeconds(-30);
            var expires = DateTime.UtcNow.AddMinutes(_expiresInMinutes);

            // Create JWT token
            var token = new JwtSecurityToken(
                issuer: _issuer,
                audience: _audience,
                notBefore: notBefore,
                expires: expires,
                claims: claims,
                signingCredentials: signingCredentials
            );

            string tokenString = new JwtSecurityTokenHandler().WriteToken(token);
            _logger.LogInformation("Access Token generated successfully for User ID: {UserId}, expires at {ExpiresAt}",
                user.Id, expires);

            return Result<string>.Ok(tokenString, "Access Token generated successfully");
        }
    }
}