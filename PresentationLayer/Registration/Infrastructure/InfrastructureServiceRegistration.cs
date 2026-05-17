using E_Commerce.Registration.AdminOperationServices;
using E_Commerce.Registration.BackgroundJobs;
using E_Commerce.Registration.CacheServices;
using E_Commerce.Registration.CollectionServices;
using E_Commerce.Registration.CustomerAddressServices;
using E_Commerce.Registration.EmailServices;
using E_Commerce.Registration.ProductServices;
using E_Commerce.Registration.SubCategoryServices;
using E_Commerce.Registration.UserOperationServices;
using E_Commerce.Registration.WishlistServices;
using Infrastructure.Interfaces;
using Infrastructure.Repository;
using Infrastructure.UOW;

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


