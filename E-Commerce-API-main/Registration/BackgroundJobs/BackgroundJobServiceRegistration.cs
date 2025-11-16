
using DomainLayer.BackgroundJops;
using Microsoft.Extensions.DependencyInjection;

namespace E_Commerce.Registration.BackgroundJobs
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


