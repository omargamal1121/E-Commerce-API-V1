using Domain.Models;

namespace Infrastructure.Interfaces
{
    public interface IProductInventoryRepository : IRepository<ProductInventory>
    {
		// Add any specific methods for ProductInventory here

		public Task<ProductInventory?> GetByInvetoryIdWithProductAsync(int id);

	}
} 

