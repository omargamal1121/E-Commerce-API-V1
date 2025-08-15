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
			var emailService = serviceProvider.GetRequiredService<IErrorNotificationService>(); // Example service

			try
			{
				logger.LogInformation("Starting data seeding...");

				var roleManager = serviceProvider.GetRequiredService<RoleManager<IdentityRole>>();
				var userManager = serviceProvider.GetRequiredService<UserManager<Customer>>();
				var paymentmethod = serviceProvider.GetRequiredService<IPaymentMethodsServices>();
				var paymentprovider = serviceProvider.GetRequiredService<IPaymentProvidersServices>();
				var uow = serviceProvider.GetRequiredService<IUnitOfWork>();

				string adminEmail = "Omargamal1132004@gmail.com";
				string adminPassword = "Admin@123";

				// Roles
				if (!await roleManager.RoleExistsAsync("Admin"))
				{
					await roleManager.CreateAsync(new IdentityRole("Admin"));
					logger.LogInformation("Role 'Admin' created.");
				}
				else logger.LogInformation("Role 'Admin' already exists.");

				if (!await roleManager.RoleExistsAsync("User"))
				{
					await roleManager.CreateAsync(new IdentityRole("User"));
					logger.LogInformation("Role 'User' created.");
				}
				else logger.LogInformation("Role 'User' already exists.");

				// Admin User
				var adminUser = await userManager.FindByEmailAsync(adminEmail);
				if (adminUser == null)
				{
					logger.LogInformation("Admin user not found. Creating...");

					adminUser = new Customer
					{
						UserName = adminEmail,
						Email = adminEmail,
						EmailConfirmed = true,
						Name = "Omar gamal",
						Age = 21,
						PhoneNumber = "01226493558",
					};

					var result = await userManager.CreateAsync(adminUser, adminPassword);
					if (result.Succeeded)
					{
						await userManager.AddToRoleAsync(adminUser, "Admin");
						logger.LogInformation("Admin user created and assigned to 'Admin' role.");
					}
					else
					{
						logger.LogError("Failed to create admin user: {Errors}", string.Join(", ", result.Errors.Select(e => e.Description)));
						return;
					}

					string? userid = await userManager.Users
						.Where(u => u.Email == adminEmail)
						.Select(x => x.Id)
						.FirstOrDefaultAsync();

					if (userid != null)
					{
						// Payment Provider
						var provider = await paymentprovider.AddIfNotExistsAsync(
							new CreatePaymentProviderDto
							{
								ApiEndpoint = "https://accept.paymob.com/",
								Hmac = "8D3993CBFCA0EEECBFBB833EEF7C5D7B",
								IframeId = "945070",
								Name = "Paymob",
								PaymentProvider = Enums.PaymentProviderEnums.Paymob,
								PublicKey = "ZXlKaGJHY2lPaUpJVXpVeE1pSXNJblI1Y0NJNklrcFhWQ0o5LmV5SmpiR0Z6Y3lJNklrMWxjbU5vWVc1MElpd2ljSEp2Wm1sc1pWOXdheUk2TVRBMk5EZ3pPU3dpYm1GdFpTSTZJbWx1YVhScFlXd2lmUS5RRVVrRWpjZXVUMDloeUhsQlNQY0JNUUdTZncwQzVndFhvR3BOZTVOdGRZX3k2VjJ2Smd6RER1WG0wN2Fzckh4NUlPTWlBQnB3bDNPRWJXREdWMWVTdw=="
							}, userid);
					await	 uow.CommitAsync();

						if (provider != null && provider.Success && provider.Data != null)
						{
							logger.LogInformation("Payment provider 'Paymob' ensured.");

							var methodResult = await paymentmethod.AddIfNotExistsAsync(
								new Createpaymentmethoddto
								{
									Integrationid = "5221967",
									IsActive = true,
									Name = "Visa",
									paymentMethod = Enums.PaymentMethodEnums.Visa,
									PaymentProviderid = provider.Data.Id
								},
								userid
							);
							await uow.CommitAsync();

							if (methodResult != null && methodResult.Success)
							{
								logger.LogInformation("Payment method 'Visa' ensured.");
							}
							else
							{
								logger.LogWarning("Failed to ensure payment method 'Visa'.");
							}
						}
						else
						{
							logger.LogWarning("Failed to ensure payment provider 'Paymob'.");
						}
					}
				}
				else
				{
					logger.LogInformation("Admin user already exists. Skipping provider/method creation.");
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
	}
}
