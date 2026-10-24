using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Application.DtoModels.ImageDtos;

namespace Application.Interfaces
{
    public interface IImageCompressor
    {
        Task<Stream> CompressAsync(Stream input, ImageCompressionOptions options, CancellationToken ct = default);
    }
}
