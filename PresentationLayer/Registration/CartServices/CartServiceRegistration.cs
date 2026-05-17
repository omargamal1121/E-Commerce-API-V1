using Application.Interfaces;
using Application.Services.CartServices;
using Infrastructure.Interfaces;
using Infrastructure.Repository;

namespace E_Commerce.Registration.CartServices
{
    public static class CartServiceRegistration
    {
        public static IServiceCollection AddCartServices(this IServiceCollection services)
        {
            // Core Cart Services
            services.AddScoped<ICartRepository,CartRepository>();
			services.AddScoped<ICartServices, Application.Services.CartServices.CartServices>();
            
            // Refactored Cart Services
            services.AddScoped<ICartCommandService, CartCommandService>();
            services.AddScoped<ICartQueryService, CartQueryService>();
            services.AddScoped<ICartCacheHelper, CartCacheHelper>();
            services.AddScoped<ICartMapper, CartMapper>();

            return services;
        }
    }
}


