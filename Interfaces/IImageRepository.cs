using E_Commerce.Models;
using E_Commerce.Services;

namespace E_Commerce.Interfaces
{
    public interface IImageRepository : IRepository<Image>
    {
        Task<Image?> GetByUrlAsync(string url);
        public void DeleteRangeImages(List<Image> images);

	}
} 