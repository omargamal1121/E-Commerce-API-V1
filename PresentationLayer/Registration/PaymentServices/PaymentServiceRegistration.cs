using Infrastructure.Interfaces;
using Application.Interfaces;
using Application.Services.PaymentServices;
using Application.Services.PaymentMethodsServices;
using Application.Services.PaymentProvidersServices;
using Application.Services.PaymentWebhookService;
using Application.Services.PayMobServices;
using Microsoft.Extensions.DependencyInjection;
using Application.Services.PaymentProccessor;
using Infrastructure.Repository;

namespace E_Commerce.Registration.PaymentServices
{
    public static class PaymentServiceRegistration
    {
        public static IServiceCollection AddPaymentServices(this IServiceCollection services)
        {
            // Core Payment Services
            services.AddScoped<IPaymentServices, Application.Services.PaymentServices.PaymentServices>();
            services.AddScoped<IPaymentRepository, PaymentRepository>();
            services.AddScoped<IPaymentMethodsServices,  Application.Services.PaymentMethodsServices.PaymentMethodsServices>();
            services.AddScoped<IPaymentProvidersServices, Application.Services.PaymentProvidersServices.PaymentProvidersServices>();
            services.AddScoped<IPaymentWebhookService, PaymentWebhookService>();
            services.AddScoped<IPayMobServices, PayMobServices>();
            services.AddScoped<IPaymentProcessor,  PayMobServices>();

            return services;
        }
    }
}


