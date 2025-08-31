using E_Commerce.Services.ProductServices;
using Microsoft.Extensions.DependencyInjection;

namespace E_Commerce.Services.ProductServices
{
    public static class ProductVariantServiceRegistration
    {
        public static IServiceCollection AddProductVariantServices(this IServiceCollection services)
        {
            // Core Product Variant Services (arranged by dependencies)
            
            // 1. Mapper (no dependencies on other variant services)
            services.AddScoped<IProductVariantMapper, ProductVariantMapper>();
            
            // 2. Cache Helper (no dependencies on other variant services)
            services.AddScoped<IProductVariantCacheHelper, ProductVariantCacheHelper>();
            
            // 3. Query Service (depends on cache helper and mapper)
            services.AddScoped<IProductVariantQueryService, ProductVariantQueryService>();
            
            // 4. Command Service (depends on cache helper and mapper)
            services.AddScoped<IProductVariantCommandService, ProductVariantCommandService>();
            
            // 5. Main Service (depends on query and command services)
            services.AddScoped<IProductVariantService, ProductVariantService>();

            return services;
        }
    }
}
