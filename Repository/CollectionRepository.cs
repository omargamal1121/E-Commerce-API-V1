using E_Commerce.Context;
using E_Commerce.Interfaces;
using E_Commerce.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace E_Commerce.Repository
{
    public class CollectionRepository : MainRepository<Collection>, ICollectionRepository
    {
        private readonly AppDbContext _context;
        private readonly ILogger<CollectionRepository> _logger;

        public CollectionRepository(AppDbContext context, ILogger<CollectionRepository> logger) 
            : base(context, logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<bool> IsExsistByName(string name)
        {
            _logger.LogInformation($"Method {IsExsistAsync} :Name {name}");
            return await _context.Collections.AnyAsync(c => c.Name.ToLower() == name.ToLower());

        }
       
		private IQueryable<E_Commerce.Models.Collection> BasicFilter(IQueryable<E_Commerce.Models.Collection> query, bool? IsActive = null, bool? IsDeleted = null)
		{
			if (IsActive.HasValue)
				query = query.Where(x => x.IsActive == IsActive.Value);
			if (IsDeleted.HasValue)
			{
				if (IsDeleted.Value)
					query = query.Where(q => q.DeletedAt != null);
				else
					query = query.Where(q => q.DeletedAt == null);


			}
			return query;
		}

		public IQueryable <Collection> GetCollectionsByName(string? name, bool? IsActive = null, bool? IsDeleted = null)
        {
            _logger.LogInformation($"Getting collection by name: {name}");
            var query = _context.Collections.AsQueryable();

			if (!string.IsNullOrWhiteSpace(name))
            query=  query.Where(c => c.Name.Contains(name));
            query =BasicFilter(query, IsActive, IsDeleted);
            return query;

        }

    

    

        public async Task<int> GetTotalCollectionCountAsync(bool? isActive = null)
        {
            var query = _context.Collections.Where(c => c.DeletedAt == null);

            if (isActive.HasValue)
            {
                query = query.Where(c => c.IsActive == isActive.Value);
            }

            return await query.CountAsync();
        }

        public async Task<bool> AddProductsToCollectionAsync(int collectionId, List<int> productIds)
        {
            _logger.LogInformation($"Adding {productIds.Count} products to collection {collectionId}");
            
            try
            {
                var existingProductCollections = await _context.ProductCollections
                    .Where(pc => pc.CollectionId == collectionId && productIds.Contains(pc.ProductId))
                    .ToListAsync();

                var existingProductIds = existingProductCollections.Select(pc => pc.ProductId).ToList();
                var newProductIds = productIds.Except(existingProductIds).ToList();

                var newProductCollections = newProductIds.Select(productId => new ProductCollection
                {
                    ProductId = productId,
                    CollectionId = collectionId
                }).ToList();

                await _context.ProductCollections.AddRangeAsync(newProductCollections);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error adding products to collection {collectionId}: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> RemoveProductsFromCollectionAsync(int collectionId, List<int> productIds)
        {
            _logger.LogInformation($"Removing {productIds.Count} products from collection {collectionId}");
            
            try
            {
                var productCollections = await _context.ProductCollections
                    .Where(pc => pc.CollectionId == collectionId && productIds.Contains(pc.ProductId))
                    .ToListAsync();

                _context.ProductCollections.RemoveRange(productCollections);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error removing products from collection {collectionId}: {ex.Message}");
                return false;
            }
        }

      

        public async Task<bool> UpdateCollectionStatusAsync(int collectionId, bool isActive)
        {
            _logger.LogInformation($"Updating collection {collectionId} status to {isActive}");
            
            try
            {
                var collection = await _context.Collections
                    .Where(c => c.Id == collectionId && c.DeletedAt == null)
                    .FirstOrDefaultAsync();

                if (collection == null)
                {
                    _logger.LogWarning($"Collection {collectionId} not found");
                    return false;
                }

                collection.IsActive = isActive;
                collection.ModifiedAt = DateTime.UtcNow;

                _context.Collections.Update(collection);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error updating collection status: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> UpdateCollectionDisplayOrderAsync(int collectionId, int displayOrder)
        {
            _logger.LogInformation($"Updating collection {collectionId} display order to {displayOrder}");
            
            try
            {
                var collection = await _context.Collections
                    .Where(c => c.Id == collectionId && c.DeletedAt == null)
                    .FirstOrDefaultAsync();

                if (collection == null)
                {
                    _logger.LogWarning($"Collection {collectionId} not found");
                    return false;
                }

                collection.DisplayOrder = displayOrder;
                collection.ModifiedAt = DateTime.UtcNow;

                _context.Collections.Update(collection);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error updating collection display order: {ex.Message}");
                return false;
            }
        }

      
        public async Task<int> GetProductCountInCollectionAsync(int collectionId)
        {
            return await _context.ProductCollections
                .Where(pc => pc.CollectionId == collectionId && pc.Product.DeletedAt == null)
                .CountAsync();
        }

      
    }
} 