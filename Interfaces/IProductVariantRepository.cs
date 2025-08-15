using E_Commerce.Enums;
using E_Commerce.Models;
using E_Commerce.Services;

namespace E_Commerce.Interfaces
{
	public interface IProductVariantRepository : IRepository<ProductVariant>
	{
		// Basic Operations
		public Task<bool> VariantExistsAsync(int id);
		public Task<ProductVariant?> GetVariantById(int id);
		public  Task<bool> IsExsistAndActive(int id);
		public Task<List<ProductVariant>> GetVariantsByProductId(int productId);
		public Task<bool> IsExsistBySizeandColor(int productid, string? color, VariantSize? size, int? wist, int? length);

		public Task<bool> UpdateVariantQuantityAsync(int variantId, int newQuantity);
		public Task<bool> ActiveVaraintAsync(int id);
		public Task<bool> DeactiveVaraintAsync(int id);

		public Task<List<ProductVariant>> GetVariantsByColorAsync(string color);

		public Task<List<ProductVariant>> GetVariantsInStockAsync();
		
	
	}
} 