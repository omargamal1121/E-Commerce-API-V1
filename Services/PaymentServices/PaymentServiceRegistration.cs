using E_Commerce.Interfaces;
using E_Commerce.Services.PaymentServices;
using E_Commerce.Services.PaymentMethodsServices;
using E_Commerce.Services.PaymentProvidersServices;
using E_Commerce.Services.PaymentWebhookService;
using E_Commerce.Services.PayMobServices;
using Microsoft.Extensions.DependencyInjection;
using E_Commerce.Services.PaymentProccessor;

namespace E_Commerce.Services.PaymentServices
{
    public static class PaymentServiceRegistration
    {
        public static IServiceCollection AddPaymentServices(this IServiceCollection services)
        {
            // Core Payment Services
            services.AddScoped<IPaymentServices, PaymentServices>();
            services.AddScoped<IPaymentMethodsServices,  PaymentMethodsServices.PaymentMethodsServices>();
            services.AddScoped<IPaymentProvidersServices, PaymentProvidersServices.PaymentProvidersServices>();
            services.AddScoped<IPaymentWebhookService, PaymentWebhookService.PaymentWebhookService>();
            services.AddScoped<IPayMobServices, PayMobServices.PayMobServices>();
            services.AddScoped<IPaymentProcessor,  PayMobServices.PayMobServices>();

            return services;
        }
    }
}
