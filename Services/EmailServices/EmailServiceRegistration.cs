using E_Commerce.Services.EmailServices;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.Extensions.DependencyInjection;

namespace E_Commerce.Services.EmailServices
{
    public static class EmailServiceRegistration
    {
        public static IServiceCollection AddEmailServices(this IServiceCollection services)
        {
            // Email Services
            services.AddTransient<IEmailSender, EmailSender>();
            
            return services;
        }
    }
}
