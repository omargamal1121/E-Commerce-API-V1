namespace Application.DtoModels.ImageDtos
{
    public sealed class ImageCompressionOptions
    {
        public int MaxWidth { get; set; } = 1600;
        public int Quality { get; set; } = 80;
        public bool RemoveMetadata { get; set; } = true;

        public static ImageCompressionOptions Default => new()
        {
            MaxWidth = 1600,
            Quality = 80,
            RemoveMetadata = true
        };

        public static ImageCompressionOptions Product => new()
        {
            MaxWidth = 1600,
            Quality = 80,
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
