using ApplicationLayer.DtoModels.ProductDtos;
using DomainLayer.Enums;
using DomainLayer.Models;
using ApplicationLayer.Services;
using static Dapper.SqlMapper;

namespace ApplicationLayer.Interfaces
{
	public interface IProductRepository:IRepository<Product>
{
		 Task<bool> RestoreProductAsync(int productId);
		public  Task UpdateProductQuntity(int productid);


		Task<Product> GetProductByIdAsync(int id, bool? isActive, bool? isDeleted);
		public  Task<bool> IsExsistByNameAsync(string name);
		public  Task<bool> IsExsistAndActiveAsync(int id) ;
		public  Task<bool> ActiveProductAsync(int productid);
		public  Task<bool> DeactiveProductAsync(int productid);
		public  Task<bool> IsExsistAndHasDiscountAsync(int id) ;
		public  Task<Discount?> GetDiscountofProduct(int productid);
		public Task<bool> RemoveDiscountFromProduct(int productid);

		public  Task<List<string>> AddDiscountToProductsAsync(List<int> productIds, int discountId);
		public  Task<bool> AddDiscountToProductAsync(int productId, int discountId);
//		public Task<List<Product>> GetBestSellersAsync(int page, int pageSize, bool? isActive = null, bool? deletedOnly = null);
		public  Task<List<Product>> GetNewArrivalsAsync(int page, int pageSize, bool? isActive = null, bool? deletedOnly = null);
		Task<Product> GetProductWithVariants(int productId);
	}
}


