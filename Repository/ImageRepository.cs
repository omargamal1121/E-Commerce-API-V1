using E_Commerce.Context;
using E_Commerce.Interfaces;
using E_Commerce.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Threading.Tasks;

namespace E_Commerce.Repository
{
    public class ImageRepository : MainRepository<Image>, IImageRepository
    {
        private readonly DbSet<Image> _images;
        private readonly ILogger<ImageRepository> _logger;

        public ImageRepository(AppDbContext context, ILogger<ImageRepository> logger) : base(context, logger)
        {
            _images = context.Images;
            _logger = logger;
        }

        public  void DeleteRangeImages(List<Image> images)
        {
            _images.RemoveRange(images);
        }
        public async Task<Image?> GetByUrlAsync(string url)
        {
            
            _logger.LogInformation($"Executing {nameof(GetByUrlAsync)} for Url: {url}");
            Image? image = await _images.SingleOrDefaultAsync(i => i.Url == url);
            if (image is null)
            {
                _logger.LogWarning($"No Image with this Url:{url}");
                return null;
            }
            _logger.LogInformation("Image found in database");
            return image;
        }
    }
} 