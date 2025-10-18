using E_Commerce.Interfaces;
using E_Commerce.Services.WishlistServices;
using Microsoft.Extensions.DependencyInjection;

namespace E_Commerce.Services.WishlistServices
{
    public static class WishlistServiceRegistration
    {
        public static IServiceCollection AddWishlistServices(this IServiceCollection services)
        {
            // Wishlist Services (arranged by dependencies)
            services.AddTransient<IWishListMapper, WishListMapper>();
            
            // 1. Cache Helper (no dependencies on other wishlist services)
            services.AddScoped<IWishlistCacheHelper, WishlistCacheHelper>();
            
            // 2. Query Service (depends on cache helper)
            services.AddScoped<IWishlistQueryService, WishlistQueryService>();
            
            // 3. Command Service (depends on cache helper)
            services.AddScoped<IWishlistCommandService, WishlistCommandService>();
            
            // 4. Main Service (depends on query and command services)
            services.AddScoped<IWishlistService, WishlistService>();

            return services;
        }
    }
}
