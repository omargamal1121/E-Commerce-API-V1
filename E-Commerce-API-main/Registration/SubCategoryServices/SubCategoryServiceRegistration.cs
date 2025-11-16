using ApplicationLayer.Interfaces;
using ApplicationLayer.Services.SubCategoryServices;
using InfrastructureLayer.Repository;
using Microsoft.Extensions.DependencyInjection;

namespace E_Commerce.Registration.SubCategoryService
{
    public static class SubCategoryServiceRegistration
    {
        public static IServiceCollection AddSubCategoryServices(this IServiceCollection services)
        {
            // SubCategory Services (arranged by dependencies)
            
            // 1. Repository and Image Service (no dependencies on other subcategory services)
            services.AddScoped<ISubCategoryRepository, SubCategoryRepository>();
            services.AddScoped<ISubCategoryImageService, SubCategoryImageService>();
            
            // 2. Cache Helper and Mapper (no dependencies on other subcategory services)
            services.AddScoped<ISubCategoryCacheHelper, SubCategoryCacheHelper>();
            services.AddScoped<ISubCategoryMapper, SubCategoryMapper>();
            
            // 3. Query and Command Services (depend on repository)
            services.AddScoped<ISubCategoryQueryService, SubCategoryQueryService>();
            services.AddScoped<ISubCategoryCommandService, SubCategoryCommandService>();
            
            // 4. Main Service (depends on query and command services)
            services.AddScoped<ISubCategoryServices, SubCategoryServices>();
            
            return services;
        }
    }
}


