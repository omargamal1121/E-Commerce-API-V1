using ApplicationLayer.Interfaces;
using ApplicationLayer.Services.OrderService;
using Microsoft.Extensions.DependencyInjection;
using InfrastructureLayer.Repository;

namespace E_Commerce.Registration.OrderService
{
    public static class OrderServiceRegistration
    {
        public static IServiceCollection AddOrderServices(this IServiceCollection services)
        {
            // Core Order Services
            services.AddScoped<IOrderRepository, OrderRepository>();
            services.AddScoped<IOrderServices, OrderServices>();
            
            // Refactored Order Services
            services.AddScoped<IOrderCommandService, OrderCommandService>();
            services.AddScoped<IOrderQueryService, OrderQueryService>();
            services.AddScoped<IOrderCacheHelper, OrderCacheHelper>();
            services.AddScoped<IOrderMapper, OrderMapper>();

            return services;
        }
    }
}


