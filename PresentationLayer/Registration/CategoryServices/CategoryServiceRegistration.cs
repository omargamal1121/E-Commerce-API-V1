using Application.Interfaces;
using Application.Services.CategoryServices;
using Infrastructure.Interfaces;
using Infrastructure.Repository;
using Microsoft.Extensions.DependencyInjection;

namespace E_Commerce.Registration.CategoryServices
{
    public static class CategoryServiceRegistration
    {
        public static IServiceCollection AddCategoryServices(this IServiceCollection services)
        {
            // Core Category Services
            services.AddScoped<ICategoryRepository, CategoryRepository>();
            services.AddScoped<ICategoryServices, Application.Services.CategoryServices.CategoryServices>();
            
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


