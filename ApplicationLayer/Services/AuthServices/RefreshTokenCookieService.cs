using Application.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;

namespace Application.Services.AuthServices
{
    
    public class RefreshTokenCookieService : IRefreshTokenCookieService
    {
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly IConfiguration _configuration;
        private readonly string _cookieName;
        private readonly TimeSpan _refreshTokenExpiry;

        public RefreshTokenCookieService(
            IHttpContextAccessor httpContextAccessor,
            IConfiguration configuration)
        {
            _httpContextAccessor = httpContextAccessor;
            _configuration = configuration;
            _cookieName = "Refresh";

            int expiryHours = _configuration.GetValue<int>("JwtSettings:RefreshTokenExpiryHours", 168); // 7 days default
            _refreshTokenExpiry = TimeSpan.FromHours(expiryHours);
        }

        public void SetRefreshToken(string token)
        {
            _httpContextAccessor?.HttpContext?.Response.Cookies.Append(
                _cookieName,
                token,
                new CookieOptions
                {
                    HttpOnly = true,
                    Secure = true,
                    SameSite = SameSiteMode.Strict,
                    Expires = DateTimeOffset.UtcNow.Add(_refreshTokenExpiry)
                });
        }

        public string? GetRefreshToken()
        {
            return _httpContextAccessor?.HttpContext?.Request.Cookies[_cookieName];
        }

        public void RemoveRefreshToken()
        {
            _httpContextAccessor?.HttpContext?.Response.Cookies.Append(
                _cookieName,
                string.Empty,
                new CookieOptions
                {
                    Expires = DateTimeOffset.UtcNow.AddDays(-1),
                    HttpOnly = true,
                    Secure = true,
                    SameSite = SameSiteMode.Strict
                });
        }
    }
}
