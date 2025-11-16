
using ApplicationLayer.Interfaces;
using DomainLayer.Models;
using InfrastructureLayer.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;
using System.Collections;
using System.Linq.Expressions;
namespace InfrastructureLayer.Repository
{
	public class ProductRepository : MainRepository<Product>, IProductRepository
{
    public async Task<bool> RestoreProductAsync(int productId)
    {
        var product = await _entity.FirstOrDefaultAsync(p => p.Id == productId && p.DeletedAt != null);
        if (product == null)
            return false;
        product.DeletedAt = null;
        return true;
    }
	
		private readonly DbSet<Product> _entity;
		private readonly ILogger<ProductRepository> _logger;

		public ProductRepository( AppDbContext context, ILogger<ProductRepository> logger) : base(context, logger)
		{
			_logger = logger;
			_entity = context.Products;
		}

	
		private IQueryable<Product> GetQueryableProducts(bool? isActive = null, bool? deletedOnly = null)
		{
			var query = _entity.AsQueryable();
			if (isActive.HasValue)
				query = query.Where(p => p.IsActive == isActive.Value);
			if (deletedOnly.HasValue)
			{
				if (deletedOnly.Value)
					query = query.Where(p => p.DeletedAt != null);
				else
					query = query.Where(p => p.DeletedAt == null);
			}
			return query;
		}
		public async Task<bool>ActiveProductAsync(int productid)
		{
			 var product= await _entity.FirstOrDefaultAsync(p => p.Id == productid && p.DeletedAt == null && p.IsActive==false);
			if (product == null)
				return false;
			product.IsActive = true;
			return true;

		}

		public async Task UpdateProductQuntity(int productid)
		{
		
			
			var product= await _entity.Where(p => p.Id == productid && p.DeletedAt == null).Include(p=>p.ProductVariants).FirstOrDefaultAsync();
			if(product!=null)
			{
				product.Quantity = product.ProductVariants.Where(v => v.DeletedAt == null).Sum(v => v.Quantity);
			}
		}
		public async Task<bool>DeactiveProductAsync(int productid)
		{
			 var product= await _entity.FirstOrDefaultAsync(p => p.Id == productid && p.DeletedAt == null && p.IsActive);
			if (product == null)
				return false;
			product.IsActive = false;
			return true;

		}
		public async Task<Product?> GetProductWithSimbleDataByIdAsync(int id, bool? isActive = null, bool? deletedOnly = null)
		{ 
			var query = GetQueryableProducts(isActive, deletedOnly);
			var product = await query.FirstOrDefaultAsync(p => p.Id == id);

			return product;

		}


		public async Task<Product?> GetProductByIdAsync(int id, bool? isActive = null, bool? deletedOnly = null)
		{
			var query = GetQueryableProducts(isActive, deletedOnly);

			var product= await query.Include(i => i.Images).Include(p => p.Discount).FirstOrDefaultAsync(p=>p.Id==id);
			return product;


		}
//		public async Task<List<ProductDto>> GetBestSellersAsync(int page, int pageSize, bool? isActive = null, bool? deletedOnly = null)
//		{
//			var query = GetAll().AsNoTracking();
//			query = BasicFilter(query, isActive, deletedOnly);

//			var bestSellers = await  query
//.Select(p => new  {
//	Product = p,
//	TotalSold = p.OrderItems
//		.Where(oi => oi.Order.Status == E_Commerce.Enums.OrderStatus.Delivered)
//		.Sum(oi => (int?)oi.Quantity) ?? 0
//})
//.OrderByDescending(x => x.TotalSold)
//.Skip((page - 1) * pageSize)
//.Take(pageSize)
//.Select(x => new Product
//{
//	Id = x.Product.Id,
//	Name = x.Product.Name,
//	Description = x.Product.Description,
//	Quantity = x.Product.Quantity,	
	
//	Gender = x.Product.Gender,
//	SubCategoryId = x.Product.SubCategoryId,
//	Price = x.Product.Price,
//	CreatedAt = x.Product.CreatedAt,
//	ModifiedAt = x.Product.ModifiedAt,
//	DeletedAt = x.Product.DeletedAt,
//	fitType = x.Product.fitType,
//	Images = x.Product.Images
//		.Where(i => i.DeletedAt == null)
//		.Select(i => new Image { Id = i.Id, Url = i.Url }).ToList(),
//	IsActive = x.Product.IsActive
//}).ToListAsync();

//			return bestSellers;
//		}
		private IQueryable<DomainLayer.Models.Product> BasicFilter(IQueryable<DomainLayer.Models.Product> query, bool? isActive, bool? DeletedOnly)
		{
			if (isActive.HasValue)
			{
				if (isActive.Value)
					query = query.Where(p => p.IsActive);
				else
					query = query.Where(p => !p.IsActive);
			}
			if (DeletedOnly.HasValue)
			{
				if (DeletedOnly.Value)
					query = query.Where(p => p.DeletedAt != null);
				else
					query = query.Where(p => p.DeletedAt == null);
			}
			return query;
		}


		public async Task<List<string> > AddDiscountToProductsAsync(List<int> productIds, int discountId)
		{
			var products = await _entity
				.Where(p => productIds.Contains(p.Id) && p.DeletedAt == null)
				.ToListAsync();

			var foundProductIds = products.Select(p => p.Id).ToList();
			var notFoundIds = productIds.Except(foundProductIds).ToList();
			var warnings = new List<string>();
			if (!foundProductIds.Any()){
				warnings.Add("No product found for the given IDs.");
				return warnings;
			}
			;
				

			foreach (var product in products)
			{
				product.DiscountId = discountId;
			}

			if (notFoundIds.Any())
			{
				warnings.Add($"The following product IDs were not found or deleted: {string.Join(", ", notFoundIds)}");
			}

			return  warnings;
		}

		public async Task<List<Product>> GetNewArrivalsAsync(int page, int pageSize, bool? isActive = null, bool? deletedOnly = null)
		{

			var thirtyDaysAgo = DateTime.UtcNow.AddDays(-90);
			var query = GetAll().AsNoTracking().Where(p => p.CreatedAt >= thirtyDaysAgo);

			query = BasicFilter(query, isActive, deletedOnly);
			var products = await query
				.Skip((page - 1) * pageSize).OrderBy(p=>p.CreatedAt)
				.Take(pageSize)
				.OrderByDescending(p => p.CreatedAt)
				.ToListAsync();

			
			return products;


		}

		public async Task< bool> IsExsistByNameAsync(string name)=> await _entity.AnyAsync(p => p.Name == name);
		public async Task< bool> IsExsistAndActiveAsync(int id)=> await _entity.AnyAsync(p => p.Id==id&&p.IsActive&&p.DeletedAt==null);
		public async Task< bool> IsExsistAndHasDiscountAsync(int id)=> await _entity.AnyAsync(p => p.Id==id&&p.DiscountId!=null&&p.DeletedAt==null);
		
		public async Task<Product> GetProductWithVariants(int productId)
		{
			return await _entity
				.Include(p => p.ProductVariants)
				.FirstOrDefaultAsync(p => p.Id == productId && p.DeletedAt == null);
		}
		public async Task<bool> RemoveDiscountFromProduct(int productid)
		{
			var product = await _entity.FirstOrDefaultAsync(p => p.Id == productid);
			if (product == null|| product.Discount == null)
				return false;
			product.Discount = null;
			return true;
		}
		public async Task<Discount?> GetDiscountofProduct(int productid)
		{
		  return await	_entity.Where(p => p.Id == productid && p.DeletedAt == null).Select(p => p.Discount).FirstOrDefaultAsync();
			

		}
		public async Task<bool> AddDiscountToProductAsync(int productId, int discountId)
		{
			var product= await _entity.FirstOrDefaultAsync(p=>p.Id== productId && p.DeletedAt==null);
			if(product==null) return false;
			product.DiscountId= discountId;
			return true;
		}
	}
}
