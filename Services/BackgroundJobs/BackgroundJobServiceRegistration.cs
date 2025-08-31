using E_Commerce.BackgroundJops;
using Microsoft.Extensions.DependencyInjection;

namespace E_Commerce.BackgroundJops
{
    public static class BackgroundJobServiceRegistration
    {
        public static IServiceCollection AddBackgroundJobServices(this IServiceCollection services)
        {
            // Background Job Services
            services.AddScoped<CategoryCleanupService>();
            
            return services;
        }
    }
}
