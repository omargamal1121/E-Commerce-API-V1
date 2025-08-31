using E_Commerce.Services.AdminOperationServices;
using Microsoft.Extensions.DependencyInjection;

namespace E_Commerce.Services.AdminOpreationServices
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
