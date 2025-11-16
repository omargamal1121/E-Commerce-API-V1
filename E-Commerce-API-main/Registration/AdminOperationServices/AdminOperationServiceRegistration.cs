using ApplicationLayer.Services.AdminOperationServices;
using Microsoft.Extensions.DependencyInjection;

namespace E_Commerce.Registration.AdminOperationServices
{
    public static class AdminOperationServiceRegistration
    {
        public static IServiceCollection AddAdminOperationServices(this IServiceCollection services)
        {
            // Admin Operation Services
            services.AddScoped<IAdminOpreationServices, AdminOpreationService>();
            
            return services;
        }
    }
}


