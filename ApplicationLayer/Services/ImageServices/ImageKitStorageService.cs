using Application.DtoModels.ImageDtos;
using Application.Exceptions;
using Application.Interfaces;
using Imagekit;
using Imagekit.Exceptions;
using Imagekit.Models.Files;
using Imagekit.Models.Files.Bulk;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ImagekitFormat = Imagekit.Models.Format;
using ImagekitSrcOptions = Imagekit.Models.SrcOptions;
using ImagekitTransformation = Imagekit.Models.Transformation;

namespace Application.Services.ImageServices
{
    public class ImageKitStorageService : IImageStorageService
    {
        private readonly IImageKitClient _imageKitClient;
        private readonly IImageCompressor _compressor;
        private readonly ImageKitSettings _settings;
        private readonly ILogger<ImageKitStorageService> _logger;
        private readonly int TargetSize = 1600;

        // Hard pixel limit: ~8 MP (e.g. 3264×2448) prevents decode of giant canvases
        private const long MaxPixels = 8_000_000;

        // Maximum images accepted in a single UploadManyAsync call
        private const int MaxImagesPerBatch = 5;

        private static readonly HashSet<string> AllowedMimeTypes = new(StringComparer.OrdinalIgnoreCase)
        {
            "image/jpeg",
            "image/png",
            "image/webp"
        };

        public ImageKitStorageService(
            IImageKitClient imageKitClient,
            IImageCompressor compressor,
            IOptions<ImageKitSettings> settings,
            ILogger<ImageKitStorageService> logger)
        {
            _imageKitClient = imageKitClient ?? throw new System.ArgumentNullException(nameof(imageKitClient));
            _compressor = compressor ?? throw new System.ArgumentNullException(nameof(compressor));
            _settings = settings?.Value ?? throw new System.ArgumentNullException(nameof(settings));
            _logger = logger ?? throw new System.ArgumentNullException(nameof(logger));
        }

        public async Task<ImageUploadResult> UploadAsync(
            Stream image,
            string originalFileName,
            string folder,
            CancellationToken ct = default)
        {
            System.ArgumentNullException.ThrowIfNull(image);

            // 1. Reject empty streams
            if (image.CanSeek && image.Length == 0)
            {
                throw new ImageValidationException("Cannot upload an empty image stream.");
            }

            // 2. Enforce max raw upload size (safety net; frontend pre-compresses)
            if (image.CanSeek && image.Length > _settings.MaxInputFileSizeBytes)
            {
                long maxMb = _settings.MaxInputFileSizeBytes / (1024 * 1024);
                throw new ImageValidationException(
                    $"File size ({image.Length / (1024 * 1024)} MB) exceeds the maximum allowed size of {maxMb} MB.");
            }

            if (image.CanSeek)
            {
                image.Position = 0;
            }

            // 3. Detect and validate image format
            var imageInfo = await SixLabors.ImageSharp.Image.IdentifyAsync(image, ct);
            if (imageInfo == null || !AllowedMimeTypes.Contains(imageInfo.Metadata.DecodedImageFormat?.DefaultMimeType ?? string.Empty))
            {
                string detected = imageInfo?.Metadata.DecodedImageFormat?.Name ?? "Unknown";
                throw new ImageValidationException($"Unsupported image format: '{detected}'. Only JPEG, PNG, and WebP are allowed.");
            }

            // 4. Enforce pixel limit to prevent very large canvas decode
            if ((long)imageInfo.Width * imageInfo.Height > MaxPixels)
            {
                throw new ImageValidationException(
                    "Image dimensions are too large. Maximum is about 8 megapixels (e.g. 3264x2448).");
            }

            // Reset stream position after IdentifyAsync so the compressor reads from the start
            if (image.CanSeek)
            {
                image.Position = 0;
            }

            var options = new ImageCompressionOptions
            {
                MaxWidth = TargetSize,
                Quality = 80,
                RemoveMetadata = true,
            };

            using var compressedStream = await _compressor.CompressAsync(image, options, ct);

            string safeFileName = $"{System.Guid.NewGuid():N}.webp";
            string targetFolder = string.IsNullOrWhiteSpace(folder)
                ? _settings.DefaultFolder
                : "/" + folder.Trim().Trim('/');

            try
            {
                var uploadParams = new FileUploadParams
                {
                    File = compressedStream,
                    FileName = safeFileName,
                    Folder = targetFolder,
                    UseUniqueFileName = true
                };

                _logger.LogInformation("Uploading image {FileName} to ImageKit folder {Folder}...", safeFileName, targetFolder);

                FileUploadResponse response = await _imageKitClient.Files.Upload(uploadParams, cancellationToken: ct);

                _logger.LogInformation("Successfully uploaded image {FileName}. FileId: {FileId}, Url: {Url}",
                    safeFileName, response.FileID, response.Url);

                return new ImageUploadResult(
                    Url: response.Url,
                    FileId: response.FileID,
                    FilePath: response.FilePath,
                    SizeInBytes: (long)(response.Size ?? 0),
                    Width: response.Width.HasValue ? (int?)System.Math.Round(response.Width.Value) : null,
                    Height: response.Height.HasValue ? (int?)System.Math.Round(response.Height.Value) : null
                );
            }
            catch (ImageKitException ex)
            {
                _logger.LogError(ex, "ImageKit API error during upload of {FileName} to {Folder}: {Message}",
                    safeFileName, targetFolder, ex.Message);
                throw new ImageStorageException($"ImageKit upload failed: {ex.Message}", ex);
            }
            catch (Exception ex) when (ex is not ImageValidationException and not ImageStorageException and not OperationCanceledException)
            {
                _logger.LogError(ex, "Unexpected error uploading image to ImageKit: {Message}", ex.Message);
                throw new ImageStorageException($"Unexpected error uploading image: {ex.Message}", ex);
            }
        }

        public async Task<IReadOnlyList<ImageUploadResult>> UploadManyAsync(
            IEnumerable<ImageUploadInput> images,
            string folder,
            CancellationToken ct = default)
        {
            System.ArgumentNullException.ThrowIfNull(images);

            var inputList = images.ToList();
            if (inputList.Count == 0)
            {
                return System.Array.Empty<ImageUploadResult>();
            }

            // Enforce a per-call image count limit to bound memory usage
            if (inputList.Count > MaxImagesPerBatch)
            {
                throw new ImageValidationException(
                    $"Too many images in a single request. Maximum is {MaxImagesPerBatch} images per upload.");
            }

            var uploadedResults = new List<ImageUploadResult>();

            try
            {
                // Process sequentially: the static gate in ImageSharpCompressor already limits
                // concurrency. Processing one-at-a-time means only one image occupies memory.
                foreach (var input in inputList)
                {
                    ct.ThrowIfCancellationRequested();
                    var result = await UploadAsync(input.Stream, input.FileName, folder, ct);
                    uploadedResults.Add(result);
                }

                return uploadedResults.AsReadOnly();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "One or more uploads failed in UploadManyAsync batch for folder {Folder}. " +
                    "Initiating best-effort cleanup of {Count} uploaded files...",
                    folder, uploadedResults.Count);

                // Best-effort rollback of files successfully uploaded before the failure
                if (uploadedResults.Count > 0)
                {
                    var fileIdsToDelete = uploadedResults.Select(r => r.FileId).ToList();
                    try
                    {
                        await DeleteManyAsync(fileIdsToDelete, CancellationToken.None);
                        _logger.LogInformation("Successfully cleaned up {Count} orphaned files following batch failure.", fileIdsToDelete.Count);
                    }
                    catch (Exception cleanupEx)
                    {
                        _logger.LogWarning(cleanupEx, "Failed to clean up some uploaded files after batch failure.");
                    }
                }

                if (ex is ImageValidationException or ImageStorageException)
                {
                    throw;
                }

                throw new ImageStorageException($"Batch upload failed: {ex.Message}. Rolled back uploaded images.", ex);
            }
        }

        public async Task DeleteAsync(string fileId, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(fileId))
            {
                return;
            }

            try
            {
                _logger.LogInformation("Deleting ImageKit file with FileId: {FileId}", fileId);
                await _imageKitClient.Files.Delete(fileId, cancellationToken: ct);
                _logger.LogInformation("Successfully deleted ImageKit file: {FileId}", fileId);
            }
            catch (ImageKitNotFoundException)
            {
                _logger.LogWarning("File with FileId: {FileId} was not found on ImageKit. Skipping deletion.", fileId);
            }
            catch (ImageKitException ex)
            {
                _logger.LogError(ex, "ImageKit error while deleting file {FileId}: {Message}", fileId, ex.Message);
                throw new ImageStorageException($"Failed to delete file from ImageKit: {ex.Message}", ex);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error deleting file {FileId}: {Message}", fileId, ex.Message);
                throw new ImageStorageException($"Unexpected error deleting file: {ex.Message}", ex);
            }
        }

        public async Task DeleteManyAsync(IEnumerable<string> fileIds, CancellationToken ct = default)
        {
            System.ArgumentNullException.ThrowIfNull(fileIds);

            var idList = fileIds.Where(id => !string.IsNullOrWhiteSpace(id)).Distinct().ToList();
            if (idList.Count == 0)
            {
                return;
            }

            try
            {
                _logger.LogInformation("Bulk deleting {Count} files from ImageKit...", idList.Count);
                var bulkParams = new BulkDeleteParams
                {
                    FileIds = idList
                };

                var response = await _imageKitClient.Files.Bulk.Delete(bulkParams, cancellationToken: ct);
                _logger.LogInformation("Successfully bulk deleted {DeletedCount} files from ImageKit.",
                    response.SuccessfullyDeletedFileIds?.Count ?? idList.Count);
            }
            catch (ImageKitException ex)
            {
                _logger.LogError(ex, "ImageKit bulk delete failed: {Message}", ex.Message);
                throw new ImageStorageException($"Failed to bulk delete files from ImageKit: {ex.Message}", ex);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error during ImageKit bulk delete: {Message}", ex.Message);
                throw new ImageStorageException($"Unexpected error during bulk delete: {ex.Message}", ex);
            }
        }

        public string GetImageUrl(string filePath, int? width = null, int? height = null, int quality = 80)
        {
            if (string.IsNullOrWhiteSpace(filePath))
            {
                return string.Empty;
            }

            ImagekitTransformation tr = (width.HasValue && width.Value > 0, height.HasValue && height.Value > 0) switch
            {
                (true, true) => new ImagekitTransformation
                {
                    Quality = System.Math.Clamp(quality, 1, 100),
                    Format = ImagekitFormat.Webp,
                    Width = width!.Value,
                    Height = height!.Value
                },
                (true, false) => new ImagekitTransformation
                {
                    Quality = System.Math.Clamp(quality, 1, 100),
                    Format = ImagekitFormat.Webp,
                    Width = width!.Value
                },
                (false, true) => new ImagekitTransformation
                {
                    Quality = System.Math.Clamp(quality, 1, 100),
                    Format = ImagekitFormat.Webp,
                    Height = height!.Value
                },
                _ => new ImagekitTransformation
                {
                    Quality = System.Math.Clamp(quality, 1, 100),
                    Format = ImagekitFormat.Webp
                }
            };

            var transformations = new List<ImagekitTransformation> { tr };

            string urlEndpoint = _settings.UrlEndpoint.TrimEnd('/');
            string src = filePath.StartsWith("http", System.StringComparison.OrdinalIgnoreCase)
                ? filePath
                : $"{urlEndpoint}/{filePath.TrimStart('/')}";

            return _imageKitClient.Helper.BuildUrl(new ImagekitSrcOptions
            {
                UrlEndpoint = urlEndpoint,
                Src = src,
                Transformation = transformations
            });
        }
    }
}
