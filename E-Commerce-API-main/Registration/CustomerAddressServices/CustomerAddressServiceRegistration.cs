using ApplicationLayer.Interfaces;
using ApplicationLayer.Services.CustomerAddressServices;
using InfrastructureLayer.Repository;
using Microsoft.Extensions.DependencyInjection;

namespace E_Commerce.Registration.CustomerAddressService
{
    public static class CustomerAddressServiceRegistration
    {
        public static IServiceCollection AddCustomerAddressServices(this IServiceCollection services)
        {
            // Customer Address Services
            services.AddScoped<ICustomerAddressRepository, CustomerAddressRepository>();
            services.AddScoped<ICustomerAddressServices, CustomerAddressServices>();
            
            return services;
        }
    }
}


