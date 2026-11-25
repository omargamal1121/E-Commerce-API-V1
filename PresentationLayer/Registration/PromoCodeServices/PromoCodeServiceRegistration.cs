using Application.Services.PromoCodeServices;
using Infrastructure.Interfaces;
using Infrastructure.Repository;
using Microsoft.Extensions.DependencyInjection;

namespace E_Commerce.Registration.PromoCodeServices
{
    public static class PromoCodeServiceRegistration
    {
        public static IServiceCollection AddPromoCodeServices(this IServiceCollection services)
        {
            services.AddScoped<IPromoCodeRepository, PromoCodeRepository>();
            services.AddScoped<IPromoCodeService, PromoCodeService>();
            services.AddScoped<IPromoCodeJobService, PromoCodeJobService>();
            return services;
        }
    }
}
