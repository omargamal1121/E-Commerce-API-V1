using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Application.DtoModels.ImageDtos;
using Application.Exceptions;
using Application.Interfaces;
using Microsoft.Extensions.Logging;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Webp;
using SixLabors.ImageSharp.Processing;

namespace Application.Services.ImageServices
{
    public class ImageSharpCompressor : IImageCompressor
    {
        private readonly ILogger<ImageSharpCompressor> _logger;

        public ImageSharpCompressor(ILogger<ImageSharpCompressor> logger)
        {
            _logger = logger;
        }

        public async Task<Stream> CompressAsync(Stream input, ImageCompressionOptions options, CancellationToken ct = default)
        {
            ArgumentNullException.ThrowIfNull(input);
            ArgumentNullException.ThrowIfNull(options);

            if (input.CanSeek)
            {
                input.Position = 0;
            }

            try
            {
                _logger.LogInformation($"Before COmppress images size: {input.Length}");
                using var image = await Image.LoadAsync(input, ct);

              
                image.Mutate(ctx => ctx.AutoOrient());

              
                if (image.Width > options.MaxWidth)
                {
                    int targetWidth = options.MaxWidth;
                    int targetHeight = (int)Math.Round((double)image.Height * options.MaxWidth / image.Width);

                    image.Mutate(ctx => ctx.Resize(new ResizeOptions
                    {
                        Size = new Size(targetWidth, targetHeight),
                        Mode = ResizeMode.Max,
                        PremultiplyAlpha = true
                    }));
                }

                
                if (options.RemoveMetadata)
                {
                    image.Metadata.ExifProfile = null;
                    image.Metadata.IptcProfile = null;
                    image.Metadata.XmpProfile = null;
                }

                
                var outputStream = new MemoryStream();
                var encoder = new WebpEncoder
                {
                    Quality = Math.Clamp(options.Quality, 1, 100),
                    FileFormat = WebpFileFormatType.Lossy
                };

                await image.SaveAsWebpAsync(outputStream, encoder, ct);
                outputStream.Position = 0;

                _logger.LogInformation("Compressed image to WebP: {Width}x{Height}, OutputSize: {Size} bytes",
                    image.Width, image.Height, outputStream.Length);

                return outputStream;
            }
            catch (UnknownImageFormatException ex)
            {
                _logger.LogError(ex, "Failed to decode image during compression: Unsupported or corrupted format.");
                throw new ImageValidationException("The uploaded file is not a supported image format or is corrupted.");
            }
            catch (Exception ex) when (ex is not ImageValidationException)
            {
                _logger.LogError(ex, "Error compressing image.");
                throw new ImageStorageException($"Failed to compress image: {ex.Message}", ex);
            }
        }
    }
}
