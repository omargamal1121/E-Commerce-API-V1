namespace Application.DtoModels.ImageDtos
{
    public sealed class ImageCompressionOptions
    {
        public int MaxWidth { get; set; } = 2000;
        public int Quality { get; set; } = 82;
        public bool RemoveMetadata { get; set; } = true;

        public static ImageCompressionOptions Default => new()
        {
            MaxWidth = 2000,
            Quality = 82,
            RemoveMetadata = true
        };

        public static ImageCompressionOptions Product => new()
        {
            MaxWidth = 2000,
            Quality = 85,
            RemoveMetadata = true
        };

        public static ImageCompressionOptions Thumbnail => new()
        {
            MaxWidth = 400,
            Quality = 78,
            RemoveMetadata = true
        };
    }
}
