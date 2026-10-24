namespace Application.DtoModels.ImageDtos
{
    public sealed record ImageUploadResult(
        string Url,
        string FileId,
        string FilePath,
        long SizeInBytes,
        int? Width = null,
        int? Height = null
    );
}
