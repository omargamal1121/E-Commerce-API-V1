using E_Commerce.Interfaces;
using E_Commerce.Repository;
using E_Commerce.Services.CustomerAddress;
using Microsoft.Extensions.DependencyInjection;

namespace E_Commerce.Services.CustomerAddress
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
