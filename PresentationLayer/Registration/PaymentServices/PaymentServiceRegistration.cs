using ApplicationLayer.Interfaces;
using ApplicationLayer.Services.PaymentServices;
using ApplicationLayer.Services.PaymentMethodsServices;
using ApplicationLayer.Services.PaymentProvidersServices;
using ApplicationLayer.Services.PaymentWebhookService;
using ApplicationLayer.Services.PayMobServices;
using Microsoft.Extensions.DependencyInjection;
using ApplicationLayer.Services.PaymentProccessor;
using InfrastructureLayer.Repository;

namespace E_Commerce.Registration.PaymentService
{
    public static class PaymentServiceRegistration
    {
        public static IServiceCollection AddPaymentServices(this IServiceCollection services)
        {
            // Core Payment Services
            services.AddScoped<IPaymentServices, PaymentServices>();
            services.AddScoped<IPaymentRepository, PaymentRepository>();
            services.AddScoped<IPaymentMethodsServices,  PaymentMethodsServices>();
            services.AddScoped<IPaymentProvidersServices, PaymentProvidersServices>();
            services.AddScoped<IPaymentWebhookService, PaymentWebhookService>();
            services.AddScoped<IPayMobServices, PayMobServices>();
            services.AddScoped<IPaymentProcessor,  PayMobServices>();

            return services;
        }
    }
}


