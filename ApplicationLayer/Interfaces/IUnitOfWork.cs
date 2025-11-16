

using DomainLayer.Models;
using Microsoft.EntityFrameworkCore.Storage;
using ApplicationLayer.Interfaces;
namespace ApplicationLayer.Interfaces
{
	public interface IUnitOfWork:IDisposable 
	{
		ICategoryRepository Category { get;  }
		IProductRepository Product { get;  }
		IPaymentRepository Payment { get; }
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
