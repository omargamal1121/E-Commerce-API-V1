using E_Commerce.Context;
using E_Commerce.DtoModels.Responses;
using E_Commerce.ErrorHnadling;
using E_Commerce.Services.AccountServices.UserCaches;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

public class SecurityStampMiddleware
{
    private readonly RequestDelegate _next;
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly IUserCacheService _cache;

    public SecurityStampMiddleware(RequestDelegate next, IServiceScopeFactory serviceScopeFactory, IUserCacheService cache)
    {
        _next = next;
        _serviceScopeFactory = serviceScopeFactory;
        _cache = cache;
    }

    public async Task Invoke(HttpContext context)
    {
        string? authHeader = context.Request.Headers["Authorization"];

        if (!string.IsNullOrEmpty(authHeader) && authHeader.StartsWith("Bearer "))
        {
            string token = authHeader.Replace("Bearer ", "");
            var handler = new JwtSecurityTokenHandler();

            if (handler.CanReadToken(token))
            {
                var jwtToken = handler.ReadJwtToken(token);
                string? userId = jwtToken.Claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier)?.Value;
                string? tokenSecurityStamp = jwtToken.Claims.FirstOrDefault(c => c.Type == "SecurityStamp")?.Value;

                if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(tokenSecurityStamp))
                {
                    await WriteUnauthorizedAsync(context, "Invalid Token");
                    return;
                }

                string? cachedStamp = await _cache.GetAsync<string>(userId);

                if (string.IsNullOrEmpty(cachedStamp))
                {
                    using var scope = _serviceScopeFactory.CreateScope();
                    var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

                    cachedStamp = await dbContext.customers
                        .Where(x => x.Id == userId)
                        .Select(x => x.SecurityStamp)
                        .FirstOrDefaultAsync();

                    if (!string.IsNullOrEmpty(cachedStamp))
                        await _cache.SetAsync(userId, cachedStamp, TimeSpan.FromHours(1));
                    else
                    {
                        await WriteUnauthorizedAsync(context, "User not found");
                        return;
                    }
                }

        
                if (cachedStamp != tokenSecurityStamp)
                {
                    await WriteUnauthorizedAsync(context, "Invalid or expired token");
                    return;
                }
            }
        }

        await _next(context);
    }

    private static async Task WriteUnauthorizedAsync(HttpContext context, string message)
    {
        context.Response.StatusCode = 401;
        context.Response.ContentType = "application/json";

        var response = ApiResponse<string>.CreateErrorResponse(
            "Error",
            new ErrorResponse("Authentication", message),
            401
        );

        await context.Response.WriteAsync(JsonConvert.SerializeObject(response));
    }
}
