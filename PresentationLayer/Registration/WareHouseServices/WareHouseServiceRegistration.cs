using Application.Interfaces;
using Application.Services.WareHouseServices;
using Infrastructure.Interfaces;
using Infrastructure.Repository;


namespace E_Commerce.Registration.WareHouseServices
{
    public static class WareHouseServiceRegistration
    {
        public static IServiceCollection AddWareHouseServices(this IServiceCollection services)
        {
            // Warehouse Services
            services.AddScoped<IWareHouseRepository, WareHouseRepository>();
            services.AddScoped<IWareHouseServices, Application.Services.WareHouseServices.WareHouseServices>();

            return services;
        }
    }
}


