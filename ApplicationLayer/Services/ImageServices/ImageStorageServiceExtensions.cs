using System;
using Application.Interfaces;
using Imagekit;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Application.Services.ImageServices
{
    public static class ImageStorageServiceExtensions
    {
        public static IServiceCollection AddImageStorage(this IServiceCollection services, IConfiguration configuration)
        {
            // 1. Configure and validate ImageKitSettings with options pattern
            services.AddOptions<ImageKitSettings>()
                .Bind(configuration.GetSection(ImageKitSettings.SectionName))
                .PostConfigure(options =>
                {
                    // Fallback to environment variable IMAGEKIT_PRIVATE_KEY if not in config
                    if (string.IsNullOrWhiteSpace(options.PrivateKey))
                    {
                        var envKey = Environment.GetEnvironmentVariable("IMAGEKIT_PRIVATE_KEY");
                        if (!string.IsNullOrWhiteSpace(envKey))
                        {
                            options.PrivateKey = envKey;
                        }
                    }

                    // Fallback to environment variable IMAGE_KIT_BASE_URL or IMAGEKIT_URL_ENDPOINT
                    if (string.IsNullOrWhiteSpace(options.UrlEndpoint))
                    {
                        var envEndpoint = Environment.GetEnvironmentVariable("IMAGEKIT_URL_ENDPOINT");
                        if (!string.IsNullOrWhiteSpace(envEndpoint))
                        {
                            options.UrlEndpoint = envEndpoint;
                        }
                    }
                })
                .ValidateDataAnnotations()
                .ValidateOnStart();

            // 2. Register ImageKitClient as a Singleton
            services.AddSingleton<ImageKitClient>(sp =>
            {
                var settings = sp.GetRequiredService<IOptions<ImageKitSettings>>().Value;
                if (string.IsNullOrWhiteSpace(settings.PrivateKey))
                {
                    throw new InvalidOperationException("ImageKit PrivateKey is missing. Please configure 'ImageKitSettings:PrivateKey' or 'IMAGEKIT_PRIVATE_KEY' environment variable.");
                }

                return new ImageKitClient
                {
                    PrivateKey = settings.PrivateKey
                };
            });

            services.AddSingleton<IImageKitClient>(sp => sp.GetRequiredService<ImageKitClient>());

            // 3. Register ImageSharp compressor
            services.AddSingleton<IImageCompressor, ImageSharpCompressor>();

            // 4. Register low-level ImageKit storage service
            services.AddScoped<IImageStorageService, ImageKitStorageService>();

            // 5. Register high-level store image service (implementing IImagesServices with ImageKit flow)
            services.AddScoped<IImagesServices, ImagesServices>();

            return services;
        }
    }
}
