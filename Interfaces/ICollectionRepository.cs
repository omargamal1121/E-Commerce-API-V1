using E_Commerce.Models;

namespace E_Commerce.Interfaces
{
    public interface ICollectionRepository : IRepository<Collection>
    {
    
        public Task<bool> IsExsistByName(string name);

		IQueryable<Collection> GetCollectionsByName(string? name, bool? IsActive = null, bool? IsDeleted = null);

        Task<int> GetTotalCollectionCountAsync(bool? isActive = null);
        Task<bool> AddProductsToCollectionAsync(int collectionId, List<int> productIds);
        Task<bool> RemoveProductsFromCollectionAsync(int collectionId, List<int> productIds);
        Task<bool> UpdateCollectionStatusAsync(int collectionId, bool isActive);
        Task<bool> UpdateCollectionDisplayOrderAsync(int collectionId, int displayOrder);
     
        Task<int> GetProductCountInCollectionAsync(int collectionId);
        
    }
} 