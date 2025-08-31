using E_Commerce.Interfaces;
using E_Commerce.Repository;
using E_Commerce.Services.Order;
using Microsoft.Extensions.DependencyInjection;

namespace E_Commerce.Services.Order
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
