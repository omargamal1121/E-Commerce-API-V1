using Hangfire.Annotations;
using Hangfire.Dashboard;
using Microsoft.Extensions.Configuration;
using System.Security.Claims;

namespace ApplicationLayer.Services.HangFireAuth
{
    public class HangfireAuthFilter : IDashboardAuthorizationFilter
    {
        private readonly string _username;
        private readonly string _password;

        public HangfireAuthFilter(IConfiguration configuration)
        {
            _username = configuration["HangFireAuth:UserName"]!;
            _password = configuration["HangFireAuth:Pass"]!;
        }

        public bool Authorize(DashboardContext context)
        {
            var httpContext = context.GetHttpContext();

            string? authHeader = httpContext.Request.Headers["Authorization"];

            if (authHeader != null && authHeader.StartsWith("Basic "))
            {
                var encoded = authHeader.Substring("Basic ".Length).Trim();
                var decoded = System.Text.Encoding.UTF8.GetString(
                    Convert.FromBase64String(encoded)
                );

                var parts = decoded.Split(':', 2);

                if (parts.Length == 2 &&
                    parts[0] == _username &&
                    parts[1] == _password)
                {
                    return true; 
                }
            }

            httpContext.Response.Headers["WWW-Authenticate"] = "Basic realm=\"Hangfire Dashboard\"";
            httpContext.Response.StatusCode = 401;
            return false;
        }
    }
}



