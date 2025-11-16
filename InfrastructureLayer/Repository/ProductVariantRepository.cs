
using ApplicationLayer.Interfaces;
using DomainLayer.Enums;
using DomainLayer.Models;
using InfrastructureLayer.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System;

namespace InfrastructureLayer.Repository
{
	public class ProductVariantRepository : MainRepository<ProductVariant>, IProductVariantRepository
	{
		private readonly DbSet<ProductVariant> _entity;
		private readonly DbSet<Product> _Product_entity;
		private readonly AppDbContext _context ;
        private readonly ILogger<ProductVariantRepository> _logger;

		public ProductVariantRepository(AppDbContext context, ILogger<ProductVariantRepository> logger) : base(context, logger)
		{
			_context = context;
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
		public async Task<bool> IsExsistBySizeandColor(
		int productId,
		string? color,
		VariantSize? size,
		int? waist,
		int? length)
		{
			var query = _entity.AsNoTracking().Where(v => v.ProductId == productId);

			if (!string.IsNullOrEmpty(color))
				query = query.Where(v => v.Color == color);

			if (size.HasValue)
				query = query.Where(v => v.Size == size.Value);

			if (waist.HasValue)
				query = query.Where(v => v.Waist == waist.Value);

			if (length.HasValue)
				query = query.Where(v => v.Length == length.Value);

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

		public async Task<int> AddNewQuntityAsync(int id,int addQuantity)
		{
			_logger.LogInformation($"Adding quantity {addQuantity} to variant ID: {id}");
            var effectedrows= await _context.Database.ExecuteSqlRawAsync(
                    "UPDATE ProductVariants SET Quantity = Quantity + {0} WHERE Id = {1} AND RowVersion = @oldRowVersion",
                    addQuantity, id
                );
			if(effectedrows>0)
			{
				_logger.LogInformation($"Successfully added {addQuantity} to variant ID: {id}");
			}
			else
			{
				_logger.LogWarning($"Failed to add quantity to variant ID: {id}. Variant may not exist.");
			}
			return effectedrows;
        }
        public async Task<int> RemoveQuntityAsync(int id, int removeQuantity)
        {
            _logger.LogInformation($"Removing quantity {removeQuantity} from variant ID: {id}");

            var effectedRows = await _context.Database.ExecuteSqlRawAsync(
                "UPDATE ProductVariants SET Quantity = Quantity - {0} WHERE Id = {1} AND Quantity >= {0}",
                removeQuantity, id
            );

            if (effectedRows > 0)
            {
                _logger.LogInformation($"Successfully removed {removeQuantity} from variant ID: {id}");
            }
            else
            {
                _logger.LogWarning($"Failed to remove quantity from variant ID: {id}. Not enough stock or variant not found.");
            }

            return effectedRows;
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