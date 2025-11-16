using ApplicationLayer.Interfaces;
using E_Commerce.Registration.AdminOperationServices;
using E_Commerce.Registration.BackgroundJobs;
using E_Commerce.Registration.CacheServices;
using E_Commerce.Registration.CollectionService;
using E_Commerce.Registration.CustomerAddressService;
using E_Commerce.Registration.EmailServices;
using E_Commerce.Registration.ProductServices;
using E_Commerce.Registration.SubCategoryService;
using E_Commerce.Registration.UserOpreationService;
using E_Commerce.Registration.WishlistServices;
using InfrastructureLayer.Repository;
using InfrastructureLayer.UOW;

namespace E_Commerce.Registration.Infrastructure
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
			//services.AddWareHouseServices();
            services.AddWishlistServices();
            services.AddSubCategoryServices();
            services.AddProductServices();
            services.AddBackgroundJobServices();

            return services;
        }
    }
}


