using E_Commerce.Models;

namespace E_Commerce.Interfaces
{
    public interface ICartRepository : IRepository<Cart>
    {
    
    
        Task<bool> AddItemToCartAsync(int cartId, CartItem item);
        public  Task<Cart?> GetCartByUserIdAsync(string userId);
        public  Task<bool> IsExsistByUserId(string userid);
        public  Task<bool> IsEmptyAsync(string userid);



	  Task<bool> RemoveCartItemAsync(int cartId, int productId, int? productVariantId = null);
        Task<bool> ClearCartAsync(int cartId);
        Task<bool> CartExistsAsync(string userId);
        Task<int> GetCartItemCountAsync(string userId);   
    }
} 