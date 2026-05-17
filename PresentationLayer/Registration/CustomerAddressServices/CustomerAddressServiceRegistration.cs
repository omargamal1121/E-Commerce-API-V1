using Application.Interfaces;
using Application.Services.CustomerAddressServices;
using Infrastructure.Repository;
using Microsoft.Extensions.DependencyInjection;

namespace E_Commerce.Registration.CustomerAddressServices
{
    public static class CustomerAddressServiceRegistration
    {
        public static IServiceCollection AddCustomerAddressServices(this IServiceCollection services)
        {
            // Customer Address Services
            services.AddScoped<ICustomerAddressRepository, CustomerAddressRepository>();
            services.AddScoped<ICustomerAddressServices, Application.Services.CustomerAddressServices.CustomerAddressServices>();
            
            return services;
        }
    }
}


