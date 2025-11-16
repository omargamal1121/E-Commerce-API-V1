using ApplicationLayer.Services;
using DomainLayer.Models;

namespace ApplicationLayer.Interfaces
{
    public interface IProductInventoryRepository : IRepository<ProductInventory>
    {
		// Add any specific methods for ProductInventory here

		public Task<ProductInventory?> GetByInvetoryIdWithProductAsync(int id);

	}
} 

