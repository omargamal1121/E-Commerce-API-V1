using Domain.Models;

namespace Infrastructure.Interfaces
{
    public interface IImageRepository : IRepository<Image>
    {
        Task<Image?> GetByUrlAsync(string url);
        public void DeleteRangeImages(List<Image> images);

	}
} 

