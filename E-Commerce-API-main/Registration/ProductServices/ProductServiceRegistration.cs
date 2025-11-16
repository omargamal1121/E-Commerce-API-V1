using ApplicationLayer.Interfaces;
using ApplicationLayer.Services.ProductServices;
using ApplicationLayer.Services.ProductVariantServices;
using E_Commerce.Registration.ProductVariantServices;
using InfrastructureLayer.Repository;
using Microsoft.Extensions.DependencyInjection;

namespace E_Commerce.Registration.ProductServices
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
            services.AddScoped<IProductsServices, ProductsServices>();
            services.AddScoped<IProductLinkBuilder, E_Commerce.LinkBuilders.ProductLinkBuilder>();

            return services;
        }
    }
}


