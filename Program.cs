using CloudinaryDotNet;
using E_Commerce.BackgroundJops;
using E_Commerce.Context;
using E_Commerce.DtoModels;
using E_Commerce.DtoModels.Responses;
using E_Commerce.ErrorHnadling;
using E_Commerce.Interfaces;
using E_Commerce.Mappings;
using E_Commerce.Middleware;
using E_Commerce.Models;
using E_Commerce.Repository;
using E_Commerce.Services;
using E_Commerce.Services.AccountServices;
using E_Commerce.Services.AccountServices.AccountManagement;
using E_Commerce.Services.AccountServices.Authentication;
using E_Commerce.Services.AccountServices.Password;
using E_Commerce.Services.AccountServices.Profile;
using E_Commerce.Services.AccountServices.Registration;
using E_Commerce.Services.AccountServices.Shared;
using E_Commerce.Services.AdminOpreationServices;
using E_Commerce.Services.BackgroundServices;
using E_Commerce.Services.Cache;
using E_Commerce.Services.CartServices;
using E_Commerce.Services.CategoryServcies;
using E_Commerce.Services.CategoryServices;
using E_Commerce.Services.Collection;
using E_Commerce.Services.CustomerAddress;
using E_Commerce.Services.DiscountServices;
using E_Commerce.Services.EmailServices;
using E_Commerce.Services.Externallogin;
using E_Commerce.Services.HangFireAuth;
using E_Commerce.Services.Order;
using E_Commerce.Services.PaymentMethodsServices;
using E_Commerce.Services.PaymentProccessor;
using E_Commerce.Services.PaymentProvidersServices;
using E_Commerce.Services.PaymentServices;
using E_Commerce.Services.PaymentWebhookService;
using E_Commerce.Services.PayMobServices;
using E_Commerce.Services.ProductInventoryServices;
using E_Commerce.Services.ProductServices;
using E_Commerce.Services.SubCategoryServices;
using E_Commerce.Services.UserOpreationServices;
using E_Commerce.Services.WareHouseServices;
using E_Commerce.Services.WishlistServices;

using E_Commerce.UOW;
using Hangfire;
using Hangfire.MySql;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding.Binders;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using MySqlConnector;
using Newtonsoft.Json;
using Scalar.AspNetCore;
using Serilog;
using Serilog.AspNetCore;
using StackExchange.Redis;
using System.Reflection;
using System.Text;
using System.Threading.RateLimiting;

namespace E_Commerce
{
	public class Program
	{
		public static async Task Main(string[] args)
		{
			var builder = WebApplication.CreateBuilder(args);
			builder
				.Services.AddControllers()
				.AddNewtonsoftJson(options =>
				{
					options.SerializerSettings.ReferenceLoopHandling =
						ReferenceLoopHandling.Serialize;
				})
				.ConfigureApiBehaviorOptions(options =>
					options.SuppressModelStateInvalidFilter = true
				);
			Log.Logger = new LoggerConfiguration()
				.WriteTo.Console()
			 .WriteTo.File("Logs/log.txt", rollingInterval: RollingInterval.Day)
			 .CreateLogger();

			builder.Host.UseSerilog();
            builder.Services.AddHealthChecks();

  
            builder.Services.AddHttpContextAccessor();

			// Add Service Registrations
			builder.Services.AddCategoryServices();
			builder.Services.AddInfrastructureServices();
			builder.Services.AddCartServices();
			builder.Services.AddDiscountServices();
			builder.Services.AddPaymentServices();
			builder.Services.AddProductServices();
			builder.Services.AddAccountServices();
			builder.Services.AddOrderServices();
			builder.Services.AddScoped<IExtrenalLoginService,ExtrenalLoginService>();

            // Add Link Builders
            builder.Services.AddTransient<ICategoryLinkBuilder, CategoryLinkBuilder>();
			builder.Services.AddTransient<IProductLinkBuilder, ProductLinkBuilder>();
			builder.Services.AddTransient<IAccountLinkBuilder, AccountLinkBuilder>();
			builder.Services.AddTransient<IWareHouseLinkBuilder, WareHouseLinkBuilder>();
			builder.Services.AddTransient<ISubCategoryLinkBuilder, SubCategoryLinkBuilder>();

			builder
				.Services.AddIdentity<Customer, IdentityRole>(options =>
				{
					var passwordPolicy = builder.Configuration.GetSection("Security:PasswordPolicy");
					options.Password.RequireDigit = passwordPolicy.GetValue<bool>("RequireDigit", true);
					options.Password.RequireLowercase = passwordPolicy.GetValue<bool>("RequireLowercase", true);
					options.Password.RequireUppercase = passwordPolicy.GetValue<bool>("RequireUppercase", true);
					options.Password.RequireNonAlphanumeric = passwordPolicy.GetValue<bool>("RequireNonAlphanumeric", true);
					options.Password.RequiredLength = passwordPolicy.GetValue<int>("RequiredLength", 8);
					options.Password.RequiredUniqueChars = passwordPolicy.GetValue<int>("RequiredUniqueChars", 4);
					var lockoutPolicy = builder.Configuration.GetSection("Security:LockoutPolicy");

					options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(
						lockoutPolicy.GetValue<int>("LockoutDurationMinutes", 15)
					);
					options.Lockout.MaxFailedAccessAttempts = lockoutPolicy.GetValue<int>("MaxFailedAttempts", 5);
					options.Lockout.AllowedForNewUsers = true;
					options.User.RequireUniqueEmail = true;
					options.SignIn.RequireConfirmedEmail = true;
				})
				.AddEntityFrameworkStores<AppDbContext>()
				.AddDefaultTokenProviders();
			builder.Services.AddScoped<IImagesServices, ImagesServices>();
			builder.Services.AddScoped<IImageRepository, ImageRepository>();
			builder.Services.AddAutoMapper(typeof(MappingProfile));
			builder.Services.AddResponseCaching();
			builder.Services.Configure<CloudinarySettings>(
			builder.Configuration.GetSection("CloudinarySettings"));
			builder.Services.AddSingleton(sp =>
			{
				var config = builder.Configuration.GetSection("CloudinarySettings");
				var cloudName = config.GetValue<string>("CloudName");
				var apiKey = config.GetValue<string>("ApiKey");
				var apiSecret = config.GetValue<string>("ApiSecret");

			
				var account = new Account(cloudName, apiKey, apiSecret);
				return new Cloudinary(account);
			});


			
			var redisUrl = builder.Configuration.GetSection("ConnectionStrings:Redis").Value ?? throw new Exception();

			var uri = new Uri(redisUrl);

			var config = new ConfigurationOptions
			{
				EndPoints = { $"{uri.Host}:{uri.Port}" },
				Password = uri.UserInfo.Split(':')[1], // extract password
				Ssl = true,
				AbortOnConnectFail = false
			};


			builder.Services.AddSingleton<IConnectionMultiplexer>(
				ConnectionMultiplexer.Connect(config)
			);

			builder.Services.AddSingleton<ICacheManager, CacheManager>();
			builder.Services.AddDbContext<AppDbContext>(
				(provider, options) =>
				{
					options.UseMySql(
						builder.Configuration.GetConnectionString("ExternalDb"),
						new MySqlServerVersion(new Version(8, 0, 21))
					);
				}
			);
			builder.Services.AddHangfire(config =>
				config.UseStorage(
					new MySqlStorage(
						builder.Configuration.GetConnectionString("ExternalDb"),
						new MySqlStorageOptions
						{
							TablesPrefix = "Hangfire_",
							QueuePollInterval = TimeSpan.FromSeconds(10),
						}
					)
				)
			);
			builder.Services.AddHangfireServer();
			builder.Services.AddCors(options =>
			{
				options.AddPolicy(
					"MyPolicy",
					Options =>
					{
						Options.AllowAnyHeader().AllowAnyMethod().AllowAnyOrigin();
					}
				);
			});
			builder.Services.AddRateLimiter( options =>
			{

				options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
					RateLimitPartition.GetFixedWindowLimiter(
						partitionKey: context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
						factory: _ => new FixedWindowRateLimiterOptions
						{
							PermitLimit = 250,
							Window = TimeSpan.FromMinutes(1),
							AutoReplenishment = true
						}));

				options.AddPolicy("login", context =>
					RateLimitPartition.GetSlidingWindowLimiter(
						partitionKey: context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
						factory: _ => new SlidingWindowRateLimiterOptions
						{
							PermitLimit = 15,
							SegmentsPerWindow = 3,
							Window = TimeSpan.FromMinutes(1),
							AutoReplenishment = true
						}));


				options.AddPolicy("register", context =>
					RateLimitPartition.GetSlidingWindowLimiter(
						partitionKey: context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
						factory: _ => new SlidingWindowRateLimiterOptions
						{
							PermitLimit = 6,
							SegmentsPerWindow = 3,
							Window = TimeSpan.FromMinutes(1),
							AutoReplenishment = true
						}));

				options.AddPolicy("reset", context =>
					RateLimitPartition.GetSlidingWindowLimiter(
						partitionKey: context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
						factory: _ => new SlidingWindowRateLimiterOptions
						{
							PermitLimit = 6,
							SegmentsPerWindow = 3,
							Window = TimeSpan.FromMinutes(1),
							AutoReplenishment = true
						}));
				options.OnRejected = async (context, token) =>
				{
					context.HttpContext.Response.StatusCode = 429;
					context.HttpContext.Response.ContentType = "application/json";

					var response = ApiResponse<string>.CreateErrorResponse("Error", new ErrorResponse("Requests", "Too many request"), 429);
					await context.HttpContext.Response.WriteAsync(
						JsonConvert.SerializeObject(response),
						token
					);

				};
			});
			builder.Services.AddEndpointsApiExplorer();
			builder.Services.AddSwaggerGen(c =>
			{
				c.SwaggerDoc("v1", new OpenApiInfo { Title = "My API", Version = "v1" });

				var securityScheme = new OpenApiSecurityScheme
				{
					Name = "Authorization",
					Description = "Enter JWT Bearer token",
					Type = SecuritySchemeType.Http,
					Scheme = "bearer",
					BearerFormat = "JWT",
					In = ParameterLocation.Header,
					Reference = new OpenApiReference
					{
						Type = ReferenceType.SecurityScheme,
						Id = "Bearer",
					},
				};

				c.AddSecurityDefinition("Bearer", securityScheme);

				var securityRequirement = new OpenApiSecurityRequirement
				{
					{ securityScheme, new[] { "Bearer" } },
				};

				c.AddSecurityRequirement(securityRequirement);
			});
			builder
				.Services.AddAuthentication(options =>
				{
					options.DefaultScheme = JwtBearerDefaults.AuthenticationScheme;
					options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
					options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
				}).AddCookie( options =>
                {
                    options.Cookie.SameSite = SameSiteMode.None; // مهم للـ OAuth
                    options.Cookie.SecurePolicy = CookieSecurePolicy.Always; // HTTPS فقط
                    options.Cookie.HttpOnly = true;
                    options.ExpireTimeSpan = TimeSpan.FromMinutes(10); // Short expiration
                }).AddGoogle(o=>
				{
					o.ClientId = builder.Configuration["Security:ExternalServices:Google:ClientId"] ??throw new Exception("ClientId missing");
					o.ClientSecret = builder.Configuration["Security:ExternalServices:Google:ClientSecret"] ?? throw new Exception("ClientSecret missing");
                })
                .AddJwtBearer(options =>
				{
					options.SaveToken = true;
					options.RequireHttpsMetadata = false;
					options.TokenValidationParameters = new TokenValidationParameters
					{
						ValidateIssuer = true,
						ValidIssuer = builder.Configuration["Jwt:Issuer"],
						ValidateAudience = true,
						ValidAudience = builder.Configuration["Jwt:Audience"],
						ValidateIssuerSigningKey = true,
						IssuerSigningKey = new SymmetricSecurityKey(
							Encoding.UTF8.GetBytes(
								builder.Configuration["Jwt:Key"]
									?? throw new Exception("Key is missing")
							)
						),
						ValidateLifetime = true,
					};
				});

			var app = builder.Build();
		

			app.UseSwagger();
			app.UseSwaggerUI();


			app.UseHttpsRedirection();


			using (var scope = app.Services.CreateScope())
			{
				var services = scope.ServiceProvider;
				var dbContext = services.GetRequiredService<AppDbContext>();

				dbContext.Database.Migrate();
				await DataSeeder.SeedDataAsync(services);
				var categoryCleanupService =
					scope.ServiceProvider.GetRequiredService<CategoryCleanupService>();
				RecurringJob.AddOrUpdate(
					"Clean-Category",
					() => categoryCleanupService.DeleteOldCategories(),
					Cron.Daily
				);
			}

			app.UseRouting();
            app.UseCors("MyPolicy");
            app.UseRateLimiter();
            app.UseAuthentication();        
            app.UseAuthorization();
            app.UseUserAuthentication();
			app.UseMiddleware<SecurityStampMiddleware>();
			app.UseStaticFiles();
			app.UseResponseCaching();
            app.UseHangfireDashboard("/hangfire", new DashboardOptions
            {
                Authorization = new[] { new HangfireAuthFilter(builder.Configuration) }
            });

            app.UseAuthorization();
			app.UseEndpoints(endpoints =>
			{
				endpoints.MapControllers();
			});
            app.MapHealthChecks("/health");
            app.Run();
		}
	}
}
