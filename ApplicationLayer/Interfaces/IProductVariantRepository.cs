using DomainLayer.Enums;
using DomainLayer.Models;
using ApplicationLayer.Services;

namespace ApplicationLayer.Interfaces
{
	public interface IProductVariantRepository : IRepository<ProductVariant>
	{
		// Basic Operations
		public Task<int> AddNewQuntityAsync(int id, int addQuantity);
		public  Task<int> RemoveQuntityAsync(int id, int removeQuantity);

        public Task<bool> VariantExistsAsync(int id);
		public Task<ProductVariant?> GetVariantById(int id);
		public  Task<bool> IsExsistAndActive(int id);
		public Task<List<ProductVariant>> GetVariantsByProductId(int productId);
		public Task<bool> IsExsistBySizeandColor(int productid, string? color, VariantSize? size, int? wist, int? length);

		public Task<bool> ActiveVaraintAsync(int id);
		public Task<bool> DeactiveVaraintAsync(int id);

		public Task<List<ProductVariant>> GetVariantsByColorAsync(string color);

		public Task<List<ProductVariant>> GetVariantsInStockAsync();
		
	
	}
} 

