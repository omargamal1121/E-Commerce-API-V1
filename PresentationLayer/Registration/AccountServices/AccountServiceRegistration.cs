using Application.Interfaces;
using Application.Services.AccountServices;
using Application.Services.AccountServices.Registration;
using Application.Services.AccountServices.Shared;
using Microsoft.Extensions.DependencyInjection;
using Application.Services.EmailServices;
using Application.Services.AccountServices.UserMangment;
using Application.Services.AccountServices.Authentication;
using Application.Services.AccountServices.AccountManagement;
using Application.Services.AuthServices;
using Application.Services.AccountServices.Password;
using Application.Services.AccountServices.Profile;
using Application.Services.AccountServices.Registration;
using Application.Services.AccountServices.UserMangment;

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


