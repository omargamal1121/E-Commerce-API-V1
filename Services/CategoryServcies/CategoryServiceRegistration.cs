using E_Commerce.Interfaces;
using E_Commerce.Services.CategoryServcies;
using E_Commerce.Services.CategoryServices;
using Microsoft.Extensions.DependencyInjection;

namespace E_Commerce.Services.CategoryServcies
{
    public static class CategoryServiceRegistration
    {
        public static IServiceCollection AddCategoryServices(this IServiceCollection services)
        {
            // Core Category Services
            services.AddScoped<ICategoryRepository, CategoryRepository>();
            services.AddScoped<ICategoryServices, CategoryServices>();
            
            // Refactored Category Services
            services.AddScoped<ICategoryCommandService, CategoryCommandService>();
            services.AddScoped<ICategoryQueryService, CategoryQueryService>();
            services.AddScoped<ICategoryCacheHelper, CategoryCacheHelper>();
            services.AddScoped<ICategoryMapper, CategoryMapper>();
            services.AddScoped<ICategoryImageService, CategoryImageServices>();

            return services;
        }
    }
}
