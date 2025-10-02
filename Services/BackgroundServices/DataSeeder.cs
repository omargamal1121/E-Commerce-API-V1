using E_Commerce.Models;
using E_Commerce.Services.EmailServices;
using E_Commerce.Services.PaymentMethodsServices;
using E_Commerce.Services.PaymentProvidersServices;
using E_Commerce.UOW;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace E_Commerce.Services.BackgroundServices
{
    public static class DataSeeder
    {
        public static async Task SeedDataAsync(IServiceProvider serviceProvider)
        {
            var logger = serviceProvider.GetRequiredService<ILoggerFactory>()
                                        .CreateLogger("DataSeeder");
            var emailService = serviceProvider.GetRequiredService<IErrorNotificationService>();

            try
            {
                logger.LogInformation("Starting data seeding...");

                var roleManager = serviceProvider.GetRequiredService<RoleManager<IdentityRole>>();
                var userManager = serviceProvider.GetRequiredService<UserManager<Customer>>();
                var paymentmethod = serviceProvider.GetRequiredService<IPaymentMethodsServices>();
                var paymentprovider = serviceProvider.GetRequiredService<IPaymentProvidersServices>();
                var uow = serviceProvider.GetRequiredService<IUnitOfWork>();

                string adminEmail = "Omargamal1132004@gmail.com";
                string adminEmail2 = "Og4381146@gmail.com";
                string adminPassword = "Admin@123";

           
                await EnsureRoleExistsAsync(roleManager, "Admin", logger);
                await EnsureRoleExistsAsync(roleManager, "User", logger);
                await EnsureRoleExistsAsync(roleManager, "SuperAdmin", logger);
                await EnsureRoleExistsAsync(roleManager, "DeliveryCompany", logger);

     
                await EnsureAdminUserAsync(userManager, adminEmail, adminPassword, logger);
                await EnsureAdminUserAsync(userManager, adminEmail2, adminPassword, logger);

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
                                ApiEndpoint = "https://accept.paymob.com",
                                Hmac = "8D3993CBFCA0EEECBFBB833EEF7C5D7B",
                                IframeId = "945070",
                                Name = "Paymob",
                                PaymentProvider = Enums.PaymentProviderEnums.Paymob,
                                PublicKey = "ZXlKaGJHY2lPaUpJVXpVeE1pSXNJblI1Y0NJNklrcFhWQ0o5LmV5SmpiR0Z6Y3lJNklrMWxjbU5vWVc1MElpd2ljSEp2Wm1sc1pWOXdheUk2TVRBMk5EZ3pPU3dpYm1GdFpTSTZJbWx1YVhScFlXd2lmUS5RRVVrRWpjZXVUMDloeUhsQlNQY0JNUUdTZncwQzVndFhvR3BOZTVOdGRZX3k2VjJ2Smd6RER1WG0wN2Fzckh4NUlPTWlBQnB3bDNPRWJXREdWMWVTdw=="
                            },
                            userId);

                        await uow.CommitAsync();

                        if (provider != null && provider.Success && provider.Data != null)
                        {
                            logger.LogInformation("Payment provider 'Paymob' ensured.");

                            await AddPaymentMethodIfNotExists(paymentmethod, userId, provider.Data.Id,
                                "Visa", Enums.PaymentMethodEnums.Visa, "5221967", logger);

                            await AddPaymentMethodIfNotExists(paymentmethod, userId, provider.Data.Id,
                                "Cash On Delivery", Enums.PaymentMethodEnums.CashOnDelivery, "", logger);

                            await AddPaymentMethodIfNotExists(paymentmethod, userId, provider.Data.Id,
                                "Wallet", Enums.PaymentMethodEnums.Wallet, "5236287", logger);

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
                    "admin@yourapp.com"+
                    "Data Seeder Error"+
                    $"An error occurred during seeding: {ex.Message}\n\n{ex.StackTrace}"
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
                    Name = "Omar gamal",
                    Age = 21,
                    PhoneNumber = "01226493558",
                 
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
                                                              Enums.PaymentMethodEnums methodEnum,
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
