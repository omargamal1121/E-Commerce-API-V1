using E_Commerce.Context;
using E_Commerce.Services;
using E_Commerce.Interfaces;
using E_Commerce.Models;
using Microsoft.EntityFrameworkCore;

namespace E_Commerce.Repository
{
    public class ProductInventoryRepository : MainRepository<ProductInventory>, IProductInventoryRepository
    {
        private readonly DbSet<ProductInventory> _entity;
        private readonly ILogger<ProductInventoryRepository> _logger;

        public ProductInventoryRepository(AppDbContext context, ILogger<ProductInventoryRepository> logger) 
            : base(context, logger)
        {
            _logger = logger;
            _entity = context.ProductInventory;
        }

        public async Task<ProductInventory?> GetByInvetoryIdWithProductAsync(int id)
        {
            _logger.LogInformation($"Executing {nameof(GetByIdAsync)} for entity {typeof(ProductInventory).Name} with ID: {id}");

            var inventory = await _entity
                .Include(i => i.Product)
                    .ThenInclude(p => p.Discount)
                .Include(i => i.Product)
                    .ThenInclude(p => p.SubCategory)
                .FirstOrDefaultAsync(x => x.Id == id);

            if (inventory != null)
            {
                return inventory;
            }

            _logger.LogWarning($"No {typeof(ProductInventory).Name} with this Id:{id}");
            return null;
        }
    }
} 