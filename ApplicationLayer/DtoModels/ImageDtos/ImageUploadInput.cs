using System.IO;

namespace Application.DtoModels.ImageDtos
{
    public sealed record ImageUploadInput(
        Stream Stream,
        string FileName
    );
}
