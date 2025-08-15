using E_Commerce.Interfaces;
using E_Commerce.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.IdentityModel.Tokens;
using Newtonsoft.Json.Linq;
using StackExchange.Redis;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

namespace E_Commerce.Services
{
	public class TokenService:ITokenService
	{
		private readonly ILogger<TokenService> _logger;
		private readonly IConfiguration _config;
		private readonly UserManager<Customer> _userManager;


		public TokenService(ILogger<TokenService> logger,  IConfiguration config, UserManager<Customer> userManager)
		{
			_logger = logger;
			_userManager = userManager;
			_config = config;
		}

		public async Task<Result<string>> GenerateTokenAsync(Customer user)
		{
			_logger.LogInformation("🔐 Generating Access Token for User ID: {UserId}", user.Id);
			return await GenerateTokenInternalAsync(user);
		}

		public async Task<Result<string>> GenerateTokenAsync(string userId)
		{
			_logger.LogInformation("🔐 Generating Access Token for User ID: {UserId}", userId);

			var user = await _userManager.FindByIdAsync(userId);
			if (user == null)
			{
				_logger.LogWarning("❌ User not found for ID: {UserId}", userId);
				return Result<string>.Fail("User not found");
			}

			return await GenerateTokenInternalAsync(user);
		}

		public async Task<Result<string>> GenerateTokenInternalAsync(Customer user)
		{
			_logger.LogInformation("🔐 Generating Access Token for User ID: {UserId}", user.Id);
			await _userManager.UpdateSecurityStampAsync(user);
			// SECURITY: Validate configuration
			string secretKey = _config["Jwt:Key"] ?? throw new ArgumentNullException("Jwt:Key is missing in appsettings.json");
			string issuer = _config["Jwt:Issuer"] ?? throw new ArgumentNullException("Jwt:Issuer is missing in appsettings.json");
			string audience = _config["Jwt:Audience"] ?? throw new ArgumentNullException("Jwt:Audience is missing in appsettings.json");

			// SECURITY: Validate secret key strength
			if (secretKey.Length < 32)
			{
				throw new InvalidOperationException("JWT secret key must be at least 32 characters long");
			}

			// SECURITY: Generate cryptographically secure JTI
			var jti = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));

			List<Claim> claims = new List<Claim>()
			{
				new(JwtRegisteredClaimNames.Jti, jti),
				new(ClaimTypes.NameIdentifier, user.Id),
				new("SecurityStamp", user.SecurityStamp ?? Guid.NewGuid().ToString()),
				new("iat", DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(), ClaimValueTypes.Integer64),
			};
			foreach(var role in await _userManager.GetRolesAsync(user))
			{
				claims.Add( new Claim (ClaimTypes.Role, role));
			}

			var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey));
			var signingCredentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha512);

			if (!double.TryParse(_config["Jwt:ExpiresInMinutes"], out double expiresInMinutes))
			{
				_logger.LogWarning("⚠️ JWT ExpiresInMinutes is missing, using default (15 minutes).");
				expiresInMinutes = 15; // SECURITY: Reduced from 10 to 15 minutes
			}

			// SECURITY: Add not before claim
			var notBefore = DateTime.UtcNow.AddSeconds(-30); // Allow 30 seconds clock skew

			JwtSecurityToken token = new JwtSecurityToken(
				issuer: issuer,
				audience: audience,
				notBefore: notBefore,
				expires: DateTime.UtcNow.AddMinutes(expiresInMinutes),
				claims: claims,
				signingCredentials: signingCredentials
			);


			string tokenString = new JwtSecurityTokenHandler().WriteToken(token);
			_logger.LogInformation($"✅ Access Token generated successfully for User ID: {user.Id}");
		

			return  Result<string>.Ok(tokenString,$"✅ Access Token generated successfully for User ID: {user.Id}") ;
		}
	
	}
}
