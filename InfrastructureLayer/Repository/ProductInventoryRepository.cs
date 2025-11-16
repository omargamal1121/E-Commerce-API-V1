
using ApplicationLayer.Interfaces;
using DomainLayer.Models;
using InfrastructureLayer.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
namespace InfrastructureLayer.Repository
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