
using DomainLayer.Models;

namespace ApplicationLayer.Interfaces
{
	public interface ICategoryRepository:IRepository<Category>
	{


		Task<bool> HasSubCategoriesAsync(int categoryId);
		public Task<bool> HasImagesAsync(int categoryId);
		public  Task<bool> HasSubCategoriesActiveAsync(int categoryId);
		public  Task<bool> ActiveCategoryAsync(int categoryId);
		public  Task<bool> DeactiveCategoryAsync(int categoryId);
		public Task<bool> HasImageWithIdAsync(int categoryId, int imageid);
		public  Task<bool> IsActiveAsync(int categoryId);
		public  Task<bool> IsDeactiveAsync(int categoryId);

		public Task<bool> IsExsistsByNameAsync(string name);
		



	}
}


