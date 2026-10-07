

using Application.Interfaces;
using Application.Services.EmailServices;
using Application.Services.PaymentMethodsServices;
using Application.Services.PaymentProvidersServices;
using Domain.Enums;
using Domain.Models;
using Infrastructure.Interfaces;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Application.Services.BackgroundServices
{
    public static class DataSeeder
    {
        public static async Task SeedDataAsync(IServiceProvider serviceProvider)
        {
            var logger = serviceProvider.GetRequiredService<ILoggerFactory>()
                                        .CreateLogger("DataSeeder");
            var emailService = serviceProvider.GetRequiredService<IErrorNotificationService>();
            var configuration = serviceProvider.GetRequiredService<IConfiguration>();

            try
            {
                logger.LogInformation("Starting data seeding...");

                var roleManager = serviceProvider.GetRequiredService<RoleManager<IdentityRole>>();
                var userManager = serviceProvider.GetRequiredService<UserManager<Customer>>();
                var paymentmethod = serviceProvider.GetRequiredService<IPaymentMethodsServices>();
                var paymentprovider = serviceProvider.GetRequiredService<IPaymentProvidersServices>();
                var uow = serviceProvider.GetRequiredService<IUnitOfWork>();

                string adminEmail = configuration["DataSeeder:AdminEmail"] ?? throw new InvalidOperationException("DataSeeder:AdminEmail configuration is missing");
                string adminPassword = configuration["DataSeeder:AdminPassword"] ?? throw new InvalidOperationException("DataSeeder:AdminPassword configuration is missing");
                string adminName = configuration["DataSeeder:AdminName"] ?? throw new InvalidOperationException("DataSeeder:AdminName configuration is missing");
                int adminAge = int.TryParse(configuration["DataSeeder:AdminAge"], out var age) ? age : throw new InvalidOperationException("DataSeeder:AdminAge configuration is missing or invalid");
                string adminPhoneNumber = configuration["DataSeeder:AdminPhoneNumber"] ?? throw new InvalidOperationException("DataSeeder:AdminPhoneNumber configuration is missing");

           
                await EnsureRoleExistsAsync(roleManager, "Admin", logger);
                await EnsureRoleExistsAsync(roleManager, "User", logger);
                await EnsureRoleExistsAsync(roleManager, "SuperAdmin", logger);
                await EnsureRoleExistsAsync(roleManager, "DeliveryCompany", logger);

     
                await EnsureAdminUserAsync(userManager, adminEmail, adminPassword, adminName, adminAge, adminPhoneNumber, logger);

                var adminUser = await userManager.FindByEmailAsync(adminEmail);
                if (adminUser != null)
                {
                    string? userId = await userManager.Users
                        .Where(u => u.Email == adminEmail)
                        .Select(x => x.Id)
                        .FirstOrDefaultAsync();

                    if (userId != null)
                    {
                        var provider = await paymentprovider.AddIfNotExistsAsync(
                            new CreatePaymentProviderDto
                            {
                                ApiEndpoint = configuration["DataSeeder:PaymobApiEndpoint"] ?? throw new InvalidOperationException("DataSeeder:PaymobApiEndpoint configuration is missing"),
                                Hmac = configuration["DataSeeder:PaymobHmac"] ?? throw new InvalidOperationException("DataSeeder:PaymobHmac configuration is missing"),
                                IframeId = configuration["DataSeeder:PaymobIframeId"] ?? throw new InvalidOperationException("DataSeeder:PaymobIframeId configuration is missing"),
                                Name = "Paymob",
                                PaymentProvider = PaymentProviderEnums.Paymob,
                                PublicKey = configuration["DataSeeder:PaymobPublicKey"] ?? throw new InvalidOperationException("DataSeeder:PaymobPublicKey configuration is missing")
                            },
                            userId);

                        await uow.CommitAsync();

                        if (provider != null && provider.Success && provider.Data != null)
                        {
                            logger.LogInformation("Payment provider 'Paymob' ensured.");

                            var visaIntegrationId = configuration["DataSeeder:PaymobVisaIntegrationId"] ?? throw new InvalidOperationException("DataSeeder:PaymobVisaIntegrationId configuration is missing");
                            var walletIntegrationId = configuration["DataSeeder:PaymobWalletIntegrationId"] ?? throw new InvalidOperationException("DataSeeder:PaymobWalletIntegrationId configuration is missing");

                            await AddPaymentMethodIfNotExists(paymentmethod, userId, provider.Data.Id,
                                "Visa", PaymentMethodEnums.Visa, visaIntegrationId, logger);

                            await AddPaymentMethodIfNotExists(paymentmethod, userId, provider.Data.Id,
                                "Cash On Delivery", PaymentMethodEnums.CashOnDelivery, "", logger);

                            await AddPaymentMethodIfNotExists(paymentmethod, userId, provider.Data.Id,
                                "Wallet", PaymentMethodEnums.Wallet, walletIntegrationId, logger);

                            await uow.CommitAsync();
                        }
                        else
                        {
                            logger.LogWarning("Failed to ensure payment provider 'Paymob'.");
                        }
                    }
                }
                else
                {
                    logger.LogInformation("Admin user not found. Skipping provider/method creation.");
                }

                logger.LogInformation("Data seeding completed successfully.");
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error occurred during data seeding.");

                await emailService.SendErrorNotificationAsync(
                    $"An error occurred during data seeding: {ex.Message}",
                    ex.StackTrace
                );
            }
        }

        private static async Task EnsureRoleExistsAsync(RoleManager<IdentityRole> roleManager,
                                                        string roleName,
                                                        ILogger logger)
        {
            if (!await roleManager.RoleExistsAsync(roleName))
            {
                await roleManager.CreateAsync(new IdentityRole(roleName));
                logger.LogInformation("Role '{role}' created.", roleName);
            }
            else
            {
                logger.LogInformation("Role '{role}' already exists.", roleName);
            }
        }

        private static async Task EnsureAdminUserAsync(UserManager<Customer> userManager,
                                                       string email,
                                                       string password,
                                                       string name,
                                                       int age,
                                                       string phoneNumber,
                                                       ILogger logger)
        {
            var user = await userManager.FindByEmailAsync(email);
            if (user == null)
            {
                logger.LogInformation("Admin user {email} not found. Creating...", email);

                user = new Customer
                {
                    UserName = email,
                    Email = email,
                    EmailConfirmed = true,
                    Name = name,
                    Age = age,
                    PhoneNumber = phoneNumber,
                 
                };

                var result = await userManager.CreateAsync(user, password);
                if (result.Succeeded)
                {
                    await userManager.AddToRoleAsync(user, "SuperAdmin");
                    logger.LogInformation("SuperAdmin user {email} created and assigned to 'SuperAdmin' role.", email);
                }
                else
                {
                    logger.LogError("Failed to create SuperAdmin user {email}: {Errors}",
                        email, string.Join(", ", result.Errors.Select(e => e.Description)));
                }
            }
            else
            {
                var ishasrole= await userManager.IsInRoleAsync(user, "SuperAdmin");
                if(!ishasrole)
                {
                    await userManager.AddToRoleAsync(user, "SuperAdmin");
                    logger.LogInformation("SuperAdmin role assigned to existing user {email}.", email);

                }
               

                logger.LogInformation("Admin user {email} already exists.", email);
            }
        }

        private static async Task AddPaymentMethodIfNotExists(IPaymentMethodsServices paymentmethod,
                                                              string userId,
                                                              int providerId,
                                                              string name,
															  PaymentMethodEnums methodEnum,
                                                              string integrationId,
                                                              ILogger logger)
        {
            var result = await paymentmethod.AddIfNotExistsAsync(
                new Createpaymentmethoddto
                {
                    Integrationid = integrationId,
                    IsActive = true,
                    Name = name,
                    paymentMethod = methodEnum,
                    PaymentProviderid = providerId
                },
                userId);

            if (result != null && result.Success)
                logger.LogInformation("Payment method '{name}' ensured.", name);
            else
                logger.LogWarning("Failed to ensure payment method '{name}'.", name);
        }
    }
}


