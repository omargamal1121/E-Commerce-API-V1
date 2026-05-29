

using Domain.Models;

namespace Infrastructure.Interfaces
{
    public interface ICartRepository : IRepository<Cart>
    {

        Task<Cart?> GetCartForCheckoutWithLockAsync(string userId);

		Task<bool> AddItemToCartAsync(int cartId, CartItem item);
        public  Task<Cart?> GetCartByUserIdAsync(string userId);
        public  Task<bool> IsExsistByUserId(string userid);
        public  Task<bool> IsEmptyAsync(string userid);
        public  Task LockCartForUpdateAsnyc(int id);



      Task<bool> RemoveCartItemAsync(int cartId, int productId, int? productVariantId = null);
        Task<bool> ClearCartAsync(int cartId);
        Task<bool> CartExistsAsync(string userId);
        Task<int> GetCartItemCountAsync(string userId);   
    }
} 

