using E_Commerce.Interfaces;
using E_Commerce.Services.EmailServices;
using E_Commerce.Services.AdminOpreationServices;
using E_Commerce.Services.UserOpreationServices;
using E_Commerce.Services.CustomerAddress;
using E_Commerce.Services.WareHouseServices;
using E_Commerce.Services.WishlistServices;
using E_Commerce.Services.SubCategoryServices;
using E_Commerce.Services.ProductServices;
using E_Commerce.UOW;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Identity;
using E_Commerce.Repository;
using E_Commerce.BackgroundJops;
using E_Commerce.Services.CacheServices;
using E_Commerce.Services.CollectionService;

namespace E_Commerce.Services
{
    public static class InfrastructureServiceRegistration
    {
        public static IServiceCollection AddInfrastructureServices(this IServiceCollection services)
        {
            // Core Infrastructure
            services.AddScoped<IUnitOfWork, UnitOfWork>();
            services.AddHttpClient();
            services.AddScoped(typeof(IRepository<>), typeof(MainRepository<>));
            
            // Add all service groups using their dedicated registration methods
            services.AddCacheServices();
            services.AddAdminOperationServices();
            services.AddUserOperationServices();
            services.AddEmailServices();
            services.AddCustomerAddressServices();
            services.AddCollectionServices();
            services.AddWareHouseServices();
            services.AddWishlistServices();
            services.AddSubCategoryServices();
            services.AddProductServices();
            services.AddBackgroundJobServices();

            return services;
        }
    }
}
