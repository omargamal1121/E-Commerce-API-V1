using Application.Services.OrderServices;
using Application.Interfaces;
using Infrastructure.Repository;
using Infrastructure.Interfaces;

namespace E_Commerce.Registration.OrderServices
{
    public static class OrderServiceRegistration
    {
        public static IServiceCollection AddOrderServices(this IServiceCollection services)
        {
            // Core Order Services
            services.AddScoped<IOrderRepository, OrderRepository>();
            services.AddScoped<IOrderServices, Application.Services.OrderServices.OrderServices>();
            
            // Refactored Order Services
            services.AddScoped<IOrderCommandService, OrderCommandService>();
            services.AddScoped<IOrderQueryService, OrderQueryService>();
            services.AddScoped<IOrderCacheHelper, OrderCacheHelper>();
            services.AddScoped<IOrderMapper, OrderMapper>();

            return services;
        }
    }
}


