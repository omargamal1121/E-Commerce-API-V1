using InfrastructureLayer.Context;
using DomainLayer.Models;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;
using ApplicationLayer.Interfaces;
using InfrastructureLayer.Repository;

namespace InfrastructureLayer.UOW
	{ 
public class UnitOfWork : IUnitOfWork
{
	private readonly Dictionary<Type, object> _repositories = new();
	private readonly ILoggerFactory _loggerFactory;
	private readonly IConnectionMultiplexer _redis;
	public ICategoryRepository Category { get; }
	public ISubCategoryRepository SubCategory { get;  }
	public ICartRepository Cart { get; }
	public IOrderRepository Order { get; }
	public ICollectionRepository Collection { get; }
	public IWareHouseRepository  WareHouse { get; }
	public IProductRepository Product { get; }
	public IProductVariantRepository ProductVariant { get; }
	public IProductInventoryRepository ProductInventory { get; }
	public IImageRepository Image { get; }
	public ICustomerAddressRepository CustomerAddress { get; }
	public IPaymentRepository  Payment { get; }

	public AppDbContext context { get; }

	public UnitOfWork(
		AppDbContext dbContext,
		IPaymentRepository paymentRepository,
        IProductVariantRepository productVariant,
		ISubCategoryRepository subCategory,
		IProductRepository product,
		ICartRepository cart,
		IOrderRepository order,
		ICollectionRepository collection,
		IWareHouseRepository wareHouse,
		IProductInventoryRepository productInventory,
		IConnectionMultiplexer redis,
		ICategoryRepository category,
		ILoggerFactory loggerFactory,
		IImageRepository imageRepository,
		ICustomerAddressRepository customerAddressRepository)
	{ 
		context = dbContext;
		Payment = paymentRepository;
        ProductVariant = productVariant;
		SubCategory = subCategory;
		Product = product;
		Cart = cart;
		Order = order;
		Collection = collection;
		WareHouse = wareHouse;
		ProductInventory = productInventory;
		_redis = redis;
		Category = category;
		_loggerFactory = loggerFactory;
		Image = imageRepository;
		CustomerAddress = customerAddressRepository;
	}

	public async Task<int> CommitAsync()
	{
	
		return await context.SaveChangesAsync();
	}

	public void Dispose()
	{
		context.Dispose();
	}

	public IRepository<T> Repository<T>() where T : BaseEntity
	{
		if (!_repositories.ContainsKey(typeof(T)))
		{
		
			var logger = _loggerFactory.CreateLogger<MainRepository<T>>();

		
			var repository = new MainRepository<T>(context, logger);
			_repositories.Add(typeof(T), repository);
		}

		return (IRepository<T>)_repositories[typeof(T)];
	}
	public async Task<IDbContextTransaction> BeginTransactionAsync()
	{
		return await context.Database.BeginTransactionAsync();
	}
}
}