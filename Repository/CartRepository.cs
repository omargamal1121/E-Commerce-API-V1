using E_Commerce.Context;
using E_Commerce.Interfaces;
using E_Commerce.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace E_Commerce.Repository
{
    public class CartRepository : MainRepository<Cart>, ICartRepository
    {
        private readonly AppDbContext _context;
        private readonly ILogger<CartRepository> _logger;

        public CartRepository(AppDbContext context, ILogger<CartRepository> logger) 
            : base(context, logger)
        {
            _context = context;
            _logger = logger;
        }

     

        public async Task<Cart?> GetCartByUserIdAsync(string userId)
        {
            _logger.LogInformation($"Retrieving cart for user: {userId}");
            
            try
            {
                return await _context.Cart.Include(c=>c.Items.Where(i => i.DeletedAt == null)).FirstOrDefaultAsync(c => c.UserId == userId && c.DeletedAt == null);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error retrieving cart: {ex.Message}");
                return null;
            }
		}
        public async Task<bool> IsExsistByUserId(string userid) => await _context.Cart.AnyAsync(c => c.UserId == userid && c.DeletedAt == null);

		public async Task<bool> IsEmptyAsync(string userId)
		{
            var cart = !await _context.Cart
                .Where(i => i.UserId == userId)
                .Select(i => i.Items).AnyAsync();


            return cart; 
		}


		public async Task<bool> AddItemToCartAsync(int cartId, CartItem item)
        {
            _logger.LogInformation($"Adding item to cart: {cartId}");
            
            try
            {
               
                item.CartId = cartId;
                await _context.CartItems.AddAsync(item);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error adding item to cart: {ex.Message}");
                return false;
            }
        }


		public async Task<bool> RemoveCartItemAsync(int cartId, int productId, int? productVariantId = null)
		{
			_logger.LogInformation($"Removing item from cart: {cartId}, product: {productId}, variant: {productVariantId}");

			try
			{
				var query =  _context.CartItems
					.Where(i => i.CartId == cartId &&
											  i.ProductId == productId);

                if(productVariantId.HasValue )
                    query= query.Where(q=>q.ProductVariantId==productVariantId.Value);
                var item = await query.FirstOrDefaultAsync();
				if (item == null)
				{
					_logger.LogWarning("Cart item not found for removal");
					return false;
				}

				item.DeletedAt = DateTime.UtcNow;
			    var isdeleted=	_context.CartItems.Remove(item);
                if(isdeleted == null)
                {
                    _logger.LogError("Failed to update cart item for removal");
                    return false;
				}

				_logger.LogInformation("Cart item removed successfully");
				return true;
			}
			catch (Exception ex)
			{
				_logger.LogError($"Error removing cart item: {ex.Message}");
				return false;
			}
		}

		public async Task<bool> ClearCartAsync(int cartId)
		{
			_logger.LogInformation($"Clearing cart: {cartId}");

			try
			{
				var items = await _context.CartItems
					.Where(i => i.CartId == cartId)
					.ToListAsync();

				if (!items.Any())
				{
					_logger.LogInformation($"No items to clear for cart {cartId}");
					return true;
				}

				_context.CartItems.RemoveRange(items);

				_logger.LogInformation($"Cart {cartId} cleared successfully");
				return true;
			}
			catch (Exception ex)
			{
				_logger.LogError($"Error clearing cart {cartId}: {ex.Message}");
				return false;
			}
		}


		public async Task<bool> CartExistsAsync(string userId)
        {
            return await _context.Cart
                .AnyAsync(c => c.UserId == userId && c.DeletedAt == null);
        }

        public async Task<int> GetCartItemCountAsync(string userId)
        {
            return await _context.Cart
                .Where(c => c.UserId == userId && c.DeletedAt == null)
                .SelectMany(c => c.Items.Where(i => i.DeletedAt == null))
                .SumAsync(i => i.Quantity);
        }

     
    }
} 