using Application.Services.UserOperationServices;
using Microsoft.Extensions.DependencyInjection;

namespace E_Commerce.Registration.UserOperationServices
{
    public static class UserOperationServiceRegistration
    {
        public static IServiceCollection AddUserOperationServices(this IServiceCollection services)
        {
            // User Operation Services
            services.AddScoped<IUserOperationServices, Application.Services.UserOperationServices.UserOperationServices>();
            
            return services;
        }
    }
}


