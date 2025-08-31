using E_Commerce.Interfaces;
using E_Commerce.Repository;
using E_Commerce.Services.CartServices;
using Microsoft.Extensions.DependencyInjection;

namespace E_Commerce.Services.CartServices
{
    public static class CartServiceRegistration
    {
        public static IServiceCollection AddCartServices(this IServiceCollection services)
        {
            // Core Cart Services
            services.AddScoped<ICartRepository,CartRepository>();
            services.AddScoped<ICartServices, CartServices>();
            
            // Refactored Cart Services
            services.AddScoped<ICartCommandService, CartCommandService>();
            services.AddScoped<ICartQueryService, CartQueryService>();
            services.AddScoped<ICartCacheHelper, CartCacheHelper>();
            services.AddScoped<ICartMapper, CartMapper>();

            return services;
        }
    }
}
