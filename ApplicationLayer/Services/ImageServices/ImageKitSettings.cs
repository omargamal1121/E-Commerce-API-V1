using System.ComponentModel.DataAnnotations;

namespace Application.Services.ImageServices
{
    public class ImageKitSettings
    {
        public const string SectionName = "ImageKitSettings";

        [Required(ErrorMessage = "ImageKit PrivateKey is required.")]
        public string PrivateKey { get; set; } = string.Empty;

        [Required(ErrorMessage = "ImageKit UrlEndpoint is required.")]
        [Url(ErrorMessage = "ImageKit UrlEndpoint must be a valid URL.")]
        public string UrlEndpoint { get; set; } = string.Empty;

        public string DefaultFolder { get; set; } = "/rs/products";

        public long MaxInputFileSizeBytes { get; set; } = 15 * 1024 * 1024; // 15MB
    }
}
