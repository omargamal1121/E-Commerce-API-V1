using E_Commerce.Services;
using E_Commerce.Models;

namespace E_Commerce.Interfaces
{
    public interface IProductInventoryRepository : IRepository<ProductInventory>
    {
		// Add any specific methods for ProductInventory here

		public Task<ProductInventory?> GetByInvetoryIdWithProductAsync(int id);

	}
} 