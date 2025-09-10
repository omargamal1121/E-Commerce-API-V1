using E_Commerce.Interfaces;
using E_Commerce.Repository;
using E_Commerce.Services.ProductVariantServices;
using Microsoft.Extensions.DependencyInjection;

namespace E_Commerce.Services.ProductServices
{
    public static class ProductServiceRegistration
    {
        public static IServiceCollection AddProductServices(this IServiceCollection services)
        {
            // Core Product Services
            services.AddScoped<IProductRepository, ProductRepository>();
            services.AddScoped<IProductVariantRepository, ProductVariantRepository>();
            services.AddScoped<IProductInventoryRepository, ProductInventoryRepository>();
            
            // Product Catalog Services
            services.AddTransient<IproductMapper, ProductMapper>();
            services.AddTransient<IProductCacheManger, ProductCacheManger>();
            services.AddScoped<IProductCatalogService, ProductCatalogService>();
            services.AddScoped<IProductSearchService, ProductSearchService>();
            services.AddScoped<IProductImageService, ProductImageService>();
            
            // Product Variant Services (using the new registration method)
            services.AddProductVariantServices();
            
            services.AddScoped<IProductDiscountService, ProductDiscountService>();
            services.AddScoped<IProductInventoryService, Services.ProductInventoryServices.ProductInventoryService>();
            services.AddScoped<IProductsServices, ProductsServices>();

            return services;
        }
    }
}
