using E_Commerce.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace E_Commerce.Services.DiscountServices
{
    public static class DiscountServiceRegistration
    {
        public static IServiceCollection AddDiscountServices(this IServiceCollection services)
        {
            // Core Discount Services
            services.AddScoped<IDiscountService, DiscountService>();
            
            // Refactored Discount Services
            services.AddScoped<IDiscountCommandService, DiscountCommandService>();
            services.AddScoped<IDiscountQueryService, DiscountQueryService>();
            services.AddScoped<IDiscountCacheHelper, DiscountCacheHelper>();
            services.AddScoped<IDiscountMapper, DiscountMapper>();

            return services;
        }
    }
}
