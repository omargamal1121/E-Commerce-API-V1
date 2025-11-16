using ApplicationLayer.Interfaces;
using ApplicationLayer.Services.CollectionServices;
using InfrastructureLayer.Repository;
using Microsoft.Extensions.DependencyInjection;

namespace E_Commerce.Registration.CollectionService
{
    public static class CollectionServiceRegistration
    {
        public static IServiceCollection AddCollectionServices(this IServiceCollection services)
        {
            // Collection Services (arranged by dependencies)
            
            // 1. Cache Helper and Mapper (no dependencies on other collection services)
            services.AddTransient<ICollectionCacheHelper, CollectionCacheHelper>();
            services.AddTransient<ICollectionMapper, CollectionMapper>();
            
            // 2. Repository and Image Service (no dependencies on other collection services)
            services.AddScoped<ICollectionRepository, CollectionRepository>();
            services.AddScoped<ICollectionImageService, CollectionImageService>();
            
            // 3. Query and Command Services (depend on repository)
            services.AddScoped<ICollectionQueryService, CollectionQueryService>();
            services.AddScoped<ICollectionCommandService, CollectionCommandService>();
            
            // 4. Main Service (depends on query and command services)
            services.AddScoped<ICollectionServices, CollectionServices>();
            
            return services;
        }
    }
}


