using E_Commerce.Interfaces;
using E_Commerce.Repository;
using E_Commerce.Services.WareHouseServices;
using Microsoft.Extensions.DependencyInjection;

namespace E_Commerce.Services.WareHouseServices
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
