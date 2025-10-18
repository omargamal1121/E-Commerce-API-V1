using E_Commerce.Interfaces;
using E_Commerce.Services.AccountServices;
using E_Commerce.Services.AccountServices.Authentication;
using E_Commerce.Services.AccountServices.Registration;
using E_Commerce.Services.AccountServices.Password;
using E_Commerce.Services.AccountServices.Profile;
using E_Commerce.Services.AccountServices.AccountManagement;
using E_Commerce.Services.AccountServices.Shared;
using Microsoft.Extensions.DependencyInjection;
using E_Commerce.Services.EmailServices;
using E_Commerce.Services.AccountServices.UserMangment;
using E_Commerce.Services.AccountServices.UserCaches;

namespace E_Commerce.Services.AccountServices
{
    public static class AccountServiceRegistration
    {
        public static IServiceCollection AddAccountServices(this IServiceCollection services)
        {
            services.AddSingleton<IUserCacheService, UserCacheService>();
            // Core Account Services
            services.AddScoped<ITokenService, TokenService>();
            services.AddScoped<IUserMangerMapping,UserMangerMapping>();
            services.AddScoped<IUserQueryServiece, UserQueryServiece>();
            services.AddScoped<IUserRoleMangementService, UserRoleMangementService>();
            services.AddScoped<IUserAccountManagementService, UserAccountManagementService>();
            services.AddScoped<IRefreshTokenService, RefreshTokenService>();
            services.AddScoped<IAccountEmailService, AccountEmailService>();
            
            // Authentication & Registration
            services.AddScoped<IAuthenticationService, AuthenticationService>();
            services.AddScoped<IRegistrationService, RegistrationService>();
            services.AddScoped<IPasswordService, PasswordService>();
            services.AddScoped<IProfileService, ProfileService>();
            services.AddScoped<IAccountManagementService, AccountManagementService>();

            return services;
        }
    }
}
