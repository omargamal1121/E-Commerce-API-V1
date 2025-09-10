using E_Commerce.Context;
using E_Commerce.Interfaces;
using E_Commerce.Models;
using Microsoft.EntityFrameworkCore.Storage;

namespace E_Commerce.UOW
{
	public interface IUnitOfWork:IDisposable 
	{
		AppDbContext context { get; }
		ICategoryRepository Category { get;  }
		IProductRepository Product { get;  }
		ISubCategoryRepository SubCategory { get; }
		ICartRepository Cart { get; }
		IOrderRepository Order { get; }
		public IProductVariantRepository ProductVariant { get; }
		ICollectionRepository Collection { get; }
		IWareHouseRepository WareHouse { get; }
		IProductInventoryRepository ProductInventory { get; }
		IImageRepository Image { get; }
		ICustomerAddressRepository CustomerAddress { get; }
		public Task<IDbContextTransaction> BeginTransactionAsync();
		IRepository<T> Repository<T>() where T : BaseEntity;
		public Task<int> CommitAsync();
	}
}
