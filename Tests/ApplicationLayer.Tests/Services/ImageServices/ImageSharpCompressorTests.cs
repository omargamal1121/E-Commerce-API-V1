using System.IO;
using System.Threading.Tasks;
using Application.DtoModels.ImageDtos;
using Application.Exceptions;
using Application.Services.ImageServices;
using Microsoft.Extensions.Logging.Abstractions;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Metadata.Profiles.Exif;
using SixLabors.ImageSharp.PixelFormats;
using Xunit;

namespace ApplicationLayer.Tests.Services.ImageServices
{
    public class ImageSharpCompressorTests
    {
        private readonly ImageSharpCompressor _compressor;

        public ImageSharpCompressorTests()
        {
            _compressor = new ImageSharpCompressor(NullLogger<ImageSharpCompressor>.Instance);
        }

        private static MemoryStream CreateTestImageStream(int width, int height, bool withExif = false)
        {
            using var image = new Image<Rgba32>(width, height);
            if (withExif)
            {
                image.Metadata.ExifProfile = new ExifProfile();
                image.Metadata.ExifProfile.SetValue(ExifTag.Software, "R&S Test Suite");
            }

            var stream = new MemoryStream();
            image.SaveAsJpeg(stream);
            stream.Position = 0;
            return stream;
        }

        [Fact]
        public async Task CompressAsync_ResizesImageWiderThanMaxWidth_PreservingAspectRatio()
        {
            // Arrange: 3000 x 1500 (2:1 aspect ratio)
            using var input = CreateTestImageStream(3000, 1500);
            var options = new ImageCompressionOptions { MaxWidth = 2000, Quality = 82 };

            // Act
            using var output = await _compressor.CompressAsync(input, options);

            // Assert
            Assert.NotNull(output);
            Assert.Equal(0, output.Position);

            var info = await Image.IdentifyAsync(output);
            Assert.NotNull(info);
            Assert.Equal(2000, info.Width);
            Assert.Equal(1000, info.Height);
            Assert.Equal("WEBP", info.Metadata.DecodedImageFormat?.Name);
        }

        [Fact]
        public async Task CompressAsync_DoesNotUpscaleImageNarrowerThanMaxWidth()
        {
            // Arrange: 800 x 600 (< 2000)
            using var input = CreateTestImageStream(800, 600);
            var options = new ImageCompressionOptions { MaxWidth = 2000, Quality = 82 };

            // Act
            using var output = await _compressor.CompressAsync(input, options);

            // Assert
            var info = await Image.IdentifyAsync(output);
            Assert.NotNull(info);
            Assert.Equal(800, info.Width);
            Assert.Equal(600, info.Height);
            Assert.Equal("WEBP", info.Metadata.DecodedImageFormat?.Name);
        }

        [Fact]
        public async Task CompressAsync_RemovesExifMetadataWhenConfigured()
        {
            // Arrange
            using var input = CreateTestImageStream(1000, 800, withExif: true);
            var options = new ImageCompressionOptions { MaxWidth = 2000, Quality = 82, RemoveMetadata = true };

            // Act
            using var output = await _compressor.CompressAsync(input, options);

            // Assert
            var info = await Image.IdentifyAsync(output);
            Assert.NotNull(info);
            Assert.Null(info.Metadata.ExifProfile);
        }

        [Fact]
        public async Task CompressAsync_ThrowsValidationException_WhenStreamIsNotValidImage()
        {
            // Arrange: random corrupt text bytes
            var corruptBytes = System.Text.Encoding.UTF8.GetBytes("not-an-image-file-content");
            using var input = new MemoryStream(corruptBytes);
            var options = ImageCompressionOptions.Default;

            // Act & Assert
            await Assert.ThrowsAsync<ImageValidationException>(() =>
                _compressor.CompressAsync(input, options));
        }
    }
}
