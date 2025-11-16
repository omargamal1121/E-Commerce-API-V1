using DomainLayer.Models;
using ApplicationLayer.Services;

namespace ApplicationLayer.Interfaces
{
    public interface IImageRepository : IRepository<Image>
    {
        Task<Image?> GetByUrlAsync(string url);
        public void DeleteRangeImages(List<Image> images);

	}
} 

