using ApplicationLayer.Services.UserOpreationServices;
using Microsoft.Extensions.DependencyInjection;

namespace E_Commerce.Registration.UserOpreationService
{
    public static class UserOperationServiceRegistration
    {
        public static IServiceCollection AddUserOperationServices(this IServiceCollection services)
        {
            // User Operation Services
            services.AddScoped<IUserOpreationServices, UserOpreationServices>();
            
            return services;
        }
    }
}


