using ApplicationLayer.Services.Cache;
using ApplicationLayer.Services.EmailServices;
using Microsoft.Extensions.DependencyInjection;

namespace E_Commerce.Registration.CacheServices
{
    public static class CacheServiceRegistration
    {
        public static IServiceCollection AddCacheServices(this IServiceCollection services)
        {
            // Cache Services
            services.AddSingleton<ICacheManager, CacheManager>();
            services.AddTransient<IErrorNotificationService, ErrorNotificationService>();
            
            return services;
        }
    }
}


