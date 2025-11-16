using ApplicationLayer.Interfaces;
using InfrastructureLayer.Repository;


namespace ApplicationLayer.Services.WareHouseService
{
    public static class WareHouseServiceRegistration
    {
        public static IServiceCollection AddWareHouseServices(this IServiceCollection services)
        {
            // Warehouse Services
            services.AddScoped<IWareHouseRepository, WareHouseRepository>();
            services.AddScoped<IWareHouseServices, WareHouseServices>();

            return services;
        }
    }
}


