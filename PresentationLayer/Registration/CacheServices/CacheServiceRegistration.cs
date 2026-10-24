using Application.Services.CacheServices;
using Application.Services.EmailServices;
using Microsoft.Extensions.DependencyInjection;

namespace E_Commerce.Registration.CacheServices
{
    public static class CacheServiceRegistration
    {
        public static IServiceCollection AddCacheServices(this IServiceCollection services)
        {
            // Cache Services (ICacheManager is registered conditionally in Program.cs based on Cache:Provider)
            services.AddTransient<IErrorNotificationService, ErrorNotificationService>();
            
            return services;
        }
    }
}


