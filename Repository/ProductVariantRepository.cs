using E_Commerce.Context;
using E_Commerce.Services;
using E_Commerce.Interfaces;
using E_Commerce.Models;
using Microsoft.EntityFrameworkCore;
using E_Commerce.Enums;
using Microsoft.IdentityModel.Tokens;

namespace E_Commerce.Repository
{
	public class ProductVariantRepository : MainRepository<ProductVariant>, IProductVariantRepository
	{
		private readonly DbSet<ProductVariant> _entity;
		private readonly DbSet<Product> _Product_entity;
		private readonly ILogger<ProductVariantRepository> _logger;

		public ProductVariantRepository(AppDbContext context, ILogger<ProductVariantRepository> logger) : base(context, logger)
		{
			_Product_entity = context.Products;
			_logger = logger;
			_entity = context.ProductVariants;
		}

		// Basic Operations
		public async Task<bool> VariantExistsAsync(int id)
		{
			_logger.LogInformation($"Checking if variant exists: {id}");
			return await _entity.AnyAsync(v => v.Id == id && v.DeletedAt == null);
		}

		public async Task<ProductVariant?> GetVariantById(int id)
		{
			_logger.LogInformation($"Getting variant by ID: {id}");
			return await _entity
				.Where(v => v.Id == id && v.DeletedAt == null)
				.Include(v => v.Product)
				.FirstOrDefaultAsync();
		}

		public async Task<List<ProductVariant>> GetVariantsByProductId(int productId)
		{
			_logger.LogInformation($"Getting variants by product ID: {productId}");
			return await _entity
				.Where(v => v.ProductId == productId && v.DeletedAt == null)
				.Include(v => v.Product)
				.AsNoTracking()
				.ToListAsync();
		}
		public async Task<bool> IsExsistBySizeandColor(int productid,string? color,VariantSize?size,int? wist,int?length)
		{
			var query=_entity.AsNoTracking().Where(v => v.ProductId == productid);
			if(color.IsNullOrEmpty())
				query.Where(v => v.Color == color);
			if (size.HasValue)
				query.Where(v => v.Size == size.Value);
			if (wist.HasValue)
				query.Where(v => v.Waist == wist.Value);
			if(length.HasValue)
				query.Where(query => query.Length == length.Value);
			return await query.AnyAsync();
				
		}

		// Price Management
		public async Task<bool> UpdateVariantPriceAsync(int variantId, decimal newPrice)
		{
			_logger.LogInformation($"Updating price for variant {variantId} to {newPrice}");
			var variant = await _entity.FindAsync(variantId);
			if (variant is null)
			{
				_logger.LogWarning($"Variant ID {variantId} not found.");
				return false;
			}

			if (newPrice <= 0)
			{
				_logger.LogWarning($"Invalid price: {newPrice}. Must be greater than zero.");
				return false;
			}

	
			variant.ModifiedAt = DateTime.UtcNow;
			_logger.LogInformation($"Price updated for variant ID {variantId}");
			return true;
		}

		public async Task<bool> IsExsistAndActive(int id) => await _entity.AnyAsync(p => p.Id == id && p.IsActive && p.DeletedAt == null && p.Quantity>0);

		public async Task<bool> UpdateVariantQuantityAsync(int variantId, int newQuantity)
		{
			_logger.LogInformation($"Updating quantity for variant {variantId} to {newQuantity}");
			var variant = await _entity.FindAsync(variantId);
			if (variant is null)
			{
				_logger.LogWarning($"Variant ID {variantId} not found.");
				return false;
			}

			if (newQuantity < 0)
			{
				_logger.LogWarning($"Invalid quantity: {newQuantity}. Must be non-negative.");
				return false;
			}

			variant.Quantity = newQuantity;
			variant.ModifiedAt = DateTime.UtcNow;
			_logger.LogInformation($"Quantity updated for variant ID {variantId}");
			return true;
		}

		// Search and Filter
		public async Task<List<ProductVariant>> GetVariantsByColorAsync(string color)
		{
			_logger.LogInformation($"Getting variants by color: {color}");
			return await _entity
				.Where(v => v.Color == color && v.DeletedAt == null)
				.Include(v => v.Product)
				.AsNoTracking()
				.ToListAsync();
		}



	
		public async Task<List<ProductVariant>> GetVariantsInStockAsync()
		{
			_logger.LogInformation("Getting variants in stock");
			return await _entity
				.Where(v => v.Quantity > 0 && v.DeletedAt == null)
				.Include(v => v.Product)
				.AsNoTracking()
				.ToListAsync();
		}

		public async Task<bool> ActiveVaraintAsync(int id)
		{
			var varaint= await _entity.FirstOrDefaultAsync(v => v.Id == id && v.DeletedAt == null);
			if (varaint == null)
				return false;
			varaint.IsActive=true;
			return varaint.IsActive;
		}
		public async Task<bool> DeactiveVaraintAsync(int id)
		{
			var varaint= await _entity.FirstOrDefaultAsync(v => v.Id == id&&v.DeletedAt==null);
			if (varaint == null)
				return false;
			varaint.IsActive=false;
			return true;
		}
	}
} 