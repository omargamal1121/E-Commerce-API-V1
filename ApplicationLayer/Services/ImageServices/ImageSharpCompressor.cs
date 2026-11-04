using Application.DtoModels.ImageDtos;
using Application.Exceptions;
using Application.Interfaces;
using Microsoft.Extensions.Logging;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Formats.Webp;
using SixLabors.ImageSharp.Processing;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Application.Services.ImageServices
{
    public class ImageSharpCompressor : IImageCompressor
    {
        private readonly ILogger<ImageSharpCompressor> _logger;

        // Static gate: only one image is decoded + resized + encoded at a time across the
        // whole process. The ImageKit upload happens OUTSIDE the gate so network I/O
        // does not block future compression work.
        private static readonly SemaphoreSlim _gate = new(1, 1);

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

            // Log size only when Length is safe to read (seekable stream)
            if (input.CanSeek)
            {
                _logger.LogInformation("Before compress size: {Size} bytes", input.Length);
            }

            await _gate.WaitAsync(ct);
            try
            {
                // Do NOT set SkipMetadata — EXIF orientation must be readable so AutoOrient()
                // can correct phone portrait photos. Metadata is wiped explicitly after AutoOrient.
                var decoderOptions = new DecoderOptions
                {
                    TargetSize = new Size(options.MaxWidth, options.MaxWidth),
                    MaxFrames = 1,
                };

                using var image = await Image.LoadAsync(decoderOptions, input, ct);

                image.Mutate(ctx => ctx.AutoOrient());

                // Resize if either dimension exceeds the limit
                int max = options.MaxWidth;
                if (image.Width > max || image.Height > max)
                {
                    image.Mutate(ctx => ctx.Resize(new ResizeOptions
                    {
                        Size = new Size(max, max),
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

                MemoryStream? outputStream = null;
                try
                {
                    outputStream = new MemoryStream();
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
                catch
                {
                    // Dispose the output stream if encode fails so we don't leak it
                    outputStream?.Dispose();
                    throw;
                }
            }
            catch (UnknownImageFormatException ex)
            {
                _logger.LogError(ex, "Failed to decode image during compression: Unsupported or corrupted format.");
                throw new ImageValidationException("The uploaded file is not a supported image format or is corrupted.");
            }
            catch (Exception ex) when (ex is not ImageValidationException and not OperationCanceledException)
            {
                _logger.LogError(ex, "Error compressing image.");
                throw new ImageStorageException($"Failed to compress image: {ex.Message}", ex);
            }
            finally
            {
                _gate.Release();
            }
        }
    }
}
