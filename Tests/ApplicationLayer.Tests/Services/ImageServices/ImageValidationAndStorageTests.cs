using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Application.DtoModels.ImageDtos;
using Application.Exceptions;
using Application.Interfaces;
using Application.Services.ImageServices;
using Imagekit;
using Imagekit.Models.Files;
using Imagekit.Models.Files.Bulk;
using Imagekit.Services;
using Imagekit.Services.Files;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Xunit;

namespace ApplicationLayer.Tests.Services.ImageServices
{
    public class ImageValidationAndStorageTests
    {
        private readonly Mock<IImageKitClient> _mockImageKitClient;
        private readonly Mock<IFileService> _mockFileService;
        private readonly Mock<IBulkService> _mockBulkService;
        private readonly Mock<IImageCompressor> _mockCompressor;
        private readonly IOptions<ImageKitSettings> _options;

        public ImageValidationAndStorageTests()
        {
            _mockImageKitClient = new Mock<IImageKitClient>();
            _mockFileService = new Mock<IFileService>();
            _mockBulkService = new Mock<IBulkService>();
            _mockCompressor = new Mock<IImageCompressor>();

            _mockFileService.SetupGet(f => f.Bulk).Returns(_mockBulkService.Object);
            _mockImageKitClient.SetupGet(c => c.Files).Returns(_mockFileService.Object);

            _options = Options.Create(new ImageKitSettings
            {
                PrivateKey = "private_test_key",
                UrlEndpoint = "https://ik.imagekit.io/rsfashion",
                DefaultFolder = "/rs/products",
                MaxInputFileSizeBytes = 15 * 1024 * 1024 // 15MB
            });

            // Default mock compressor returns a valid stream
            _mockCompressor
                .Setup(c => c.CompressAsync(It.IsAny<Stream>(), It.IsAny<ImageCompressionOptions>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((Stream input, ImageCompressionOptions _, CancellationToken _) =>
                {
                    var ms = new MemoryStream();
                    input.CopyTo(ms);
                    ms.Position = 0;
                    return ms;
                });
        }

        private static MemoryStream CreateValidJpegStream(int width = 100, int height = 100)
        {
            using var image = new Image<Rgba32>(width, height);
            var stream = new MemoryStream();
            image.SaveAsJpeg(stream);
            stream.Position = 0;
            return stream;
        }

        [Fact]
        public async Task UploadAsync_RejectsEmptyStream_ThrowsImageValidationException()
        {
            // Arrange
            var service = new ImageKitStorageService(
                _mockImageKitClient.Object,
                _mockCompressor.Object,
                _options,
                NullLogger<ImageKitStorageService>.Instance
            );

            using var emptyStream = new MemoryStream();

            // Act & Assert
            var ex = await Assert.ThrowsAsync<ImageValidationException>(() =>
                service.UploadAsync(emptyStream, "empty.jpg", "products"));

            Assert.Contains("empty", ex.Message, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task UploadAsync_RejectsOversizedFile_ThrowsImageValidationException()
        {
            // Arrange: 16MB stream (exceeds 15MB limit)
            var service = new ImageKitStorageService(
                _mockImageKitClient.Object,
                _mockCompressor.Object,
                _options,
                NullLogger<ImageKitStorageService>.Instance
            );

            // Mock a stream with length 16MB without allocating actual 16MB in RAM
            var mockStream = new Mock<Stream>();
            mockStream.SetupGet(s => s.Length).Returns(16 * 1024 * 1024);
            mockStream.SetupGet(s => s.CanSeek).Returns(true);

            // Act & Assert
            var ex = await Assert.ThrowsAsync<ImageValidationException>(() =>
                service.UploadAsync(mockStream.Object, "large.jpg", "products"));

            Assert.Contains("exceeds", ex.Message, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task UploadAsync_RejectsFakeExtensionAndNonImage_ThrowsImageValidationException()
        {
            // Arrange: Text content disguised as an image
            var service = new ImageKitStorageService(
                _mockImageKitClient.Object,
                _mockCompressor.Object,
                _options,
                NullLogger<ImageKitStorageService>.Instance
            );

            byte[] textBytes = System.Text.Encoding.UTF8.GetBytes("<html><body>Malicious payload</body></html>");
            using var fakeImageStream = new MemoryStream(textBytes);

            // Act & Assert
            var ex = await Assert.ThrowsAsync<ImageValidationException>(() =>
                service.UploadAsync(fakeImageStream, "avatar.jpg", "users"));

            Assert.Contains("Unsupported image format", ex.Message, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task UploadManyAsync_CleansUpAlreadyUploadedFiles_WhenSubsequentUploadFails()
        {
            // Arrange
            var service = new ImageKitStorageService(
                _mockImageKitClient.Object,
                _mockCompressor.Object,
                _options,
                NullLogger<ImageKitStorageService>.Instance
            );

            using var stream1 = CreateValidJpegStream();
            using var stream2 = CreateValidJpegStream();

            var uploadInput1 = new ImageUploadInput(stream1, "first.jpg");
            var uploadInput2 = new ImageUploadInput(stream2, "second.jpg");

            // Setup: first upload succeeds with FileID = "uploaded_file_id_1"
            var successResponse = new FileUploadResponse
            (
                rawData: new Dictionary<string, System.Text.Json.JsonElement>()
            )
            {
                FileID = "uploaded_file_id_1",
                Url = "https://ik.imagekit.io/rsfashion/first.webp",
                FilePath = "/products/first.webp",
                Size = 1024,
                Width = 200,
                Height = 200
            };

            int callCount = 0;
            _mockFileService
                .Setup(f => f.Upload(It.IsAny<FileUploadParams>(), It.IsAny<CancellationToken>()))
                .Returns<FileUploadParams, CancellationToken>((_, _) =>
                {
                    int currentCall = Interlocked.Increment(ref callCount);
                    if (currentCall == 1)
                    {
                        return Task.FromResult(successResponse);
                    }
                    throw new Imagekit.Exceptions.ImageKitApiException("ImageKit server timeout during batch upload") { StatusCode = System.Net.HttpStatusCode.InternalServerError, ResponseBody = "Internal Server Error" };
                });

            // Setup bulk delete mock for cleanup
            List<string>? deletedIds = null;
            _mockBulkService
                .Setup(b => b.Delete(It.IsAny<BulkDeleteParams>(), It.IsAny<CancellationToken>()))
                .Callback<BulkDeleteParams, CancellationToken>((param, _) =>
                {
                    deletedIds = new List<string>(param.FileIds);
                })
                .ReturnsAsync(new BulkDeleteResponse(new Dictionary<string, System.Text.Json.JsonElement>())
                {
                    SuccessfullyDeletedFileIds = ["uploaded_file_id_1"]
                });

            // Act & Assert
            var ex = await Assert.ThrowsAsync<ImageStorageException>(() =>
                service.UploadManyAsync(new[] { uploadInput1, uploadInput2 }, "products"));

            Assert.Contains("Batch upload failed", ex.Message);

            // Verify cleanup: Bulk.Delete was called containing the successfully uploaded file id
            _mockBulkService.Verify(b => b.Delete(It.IsAny<BulkDeleteParams>(), It.IsAny<CancellationToken>()), Times.Once);
            Assert.NotNull(deletedIds);
            Assert.Contains("uploaded_file_id_1", deletedIds);
        }
    }
}
