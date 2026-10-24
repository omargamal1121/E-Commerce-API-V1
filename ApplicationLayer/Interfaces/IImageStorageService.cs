using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Application.DtoModels.ImageDtos;

namespace Application.Interfaces
{
    public interface IImageStorageService
    {
        Task<ImageUploadResult> UploadAsync(Stream image, string originalFileName, string folder, CancellationToken ct = default);
        Task<IReadOnlyList<ImageUploadResult>> UploadManyAsync(IEnumerable<ImageUploadInput> images, string folder, CancellationToken ct = default);
        Task DeleteAsync(string fileId, CancellationToken ct = default);
        Task DeleteManyAsync(IEnumerable<string> fileIds, CancellationToken ct = default);
        string GetImageUrl(string filePath, int? width = null, int? height = null, int quality = 80);
    }
}
