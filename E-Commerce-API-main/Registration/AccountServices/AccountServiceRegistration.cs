using ApplicationLayer.Interfaces;
using ApplicationLayer.Services.AccountServices;
using ApplicationLayer.Services.AccountServices.Registration;
using ApplicationLayer.Services.AccountServices.Shared;
using Microsoft.Extensions.DependencyInjection;
using ApplicationLayer.Services.EmailServices;
using ApplicationLayer.Services.AccountServices.UserMangment;
using ApplicationLayer.Services.AccountServices.Authentication;
using ApplicationLayer.Services.AccountServices.AccountManagement;
using ApplicationLayer.Services.AuthServices;
using ApplicationLayer.Services.AccountServices.Password;
using ApplicationLayer.Services.AccountServices.Profile;
using ApplicationLayer.Services.AccountServices.Registration;
using ApplicationLayer.Services.AccountServices.UserMangment;

namespace E_Commerce.Registration.AccountServices
{
    public static class AccountServiceRegistration
    {
        public static IServiceCollection AddAccountServices(this IServiceCollection services)
        {
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


