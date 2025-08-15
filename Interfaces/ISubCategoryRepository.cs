using E_Commerce.Models;
using System.Linq;
using System.Threading.Tasks;

namespace E_Commerce.Interfaces
{
    public interface ISubCategoryRepository : IRepository<SubCategory>
    {

        public  Task<bool> IsExsistAndActive(int id);
        public  Task<bool> IsExsistAndDeActive(int id);
        public Task<bool> ActiveSubCategoryAsync(int id);
        public Task<bool> DeActiveSubCategoryAsync(int id);

	
	
        public  Task<bool> IsHasActiveProduct(int subCategoryId);
        public  Task<bool> HasImagesAsync(int subCategoryId);
		public Task< bool> IsExsistByNameAsync(string name);
        Task<bool> HasProductsAsync(int subCategoryId);
    }
} 