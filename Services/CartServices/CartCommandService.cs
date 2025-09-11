using E_Commerce.DtoModels.CartDtos;
using E_Commerce.DtoModels.Responses;
using E_Commerce.Enums;
using E_Commerce.Interfaces;
using E_Commerce.Models;
using E_Commerce.Services.UserOpreationServices;
using E_Commerce.UOW;
using Hangfire;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace E_Commerce.Services.CartServices
{
    public class CartCommandService : ICartCommandService
    {
        private readonly ILogger<CartCommandService> _logger;
        private readonly IBackgroundJobClient _backgroundJobClient;
        private readonly UserManager<Customer> _userManager;
        private readonly IUnitOfWork _unitOfWork;
        private readonly ICartRepository _cartRepository;
        private readonly IUserOpreationServices  _userOpreationServices ;
        private readonly ICartCacheHelper _cacheHelper;

        public CartCommandService(
            ILogger<CartCommandService> logger,
            IBackgroundJobClient backgroundJobClient,
            UserManager<Customer> userManager,
            IUnitOfWork unitOfWork,
            ICartRepository cartRepository,
            IUserOpreationServices userOpreationServices,
            ICartCacheHelper cacheHelper)
        {
            _logger = logger;
            _backgroundJobClient = backgroundJobClient;
            _userManager = userManager;
            _unitOfWork = unitOfWork;
            _cartRepository = cartRepository;
			_userOpreationServices = userOpreationServices;
            _cacheHelper = cacheHelper;
        }

        public async Task<Result<bool>> AddItemToCartAsync(string userId, CreateCartItemDto itemDto)
        {
            _logger.LogInformation($"Adding item to cart for user: {userId}, product: {itemDto.ProductId}");

            using var transaction = await _unitOfWork.BeginTransactionAsync();
            try
            {
                var customer = await _userManager.FindByIdAsync(userId);

                if (customer == null)
                {
                    await transaction.RollbackAsync();
                    return Result<bool>.Fail($"No customer with this id:{userId}", 404);
                }

                var product = await _unitOfWork.Product.GetAll()
                    .Where(p => p.Id == itemDto.ProductId && p.DeletedAt == null && p.IsActive)
                    .Select(p => new {
                        p.Id,
                        varint = p.ProductVariants.Where(p => p.Id == itemDto.ProductVariantId && p.IsActive).Select(v => new
                        {
                            v.Id,
                            v.Quantity
                        }).FirstOrDefault(),
                        p.Price,
                        Discount = p.Discount != null ? new
                        {
                            p.Discount.EndDate,
                            p.Discount.StartDate,
                            p.Discount.DiscountPercent,
                            p.Discount.DeletedAt,
                            p.Discount.IsActive
                        } : null
                    })
                    .FirstOrDefaultAsync();

                if (product == null)
                {
                    await transaction.RollbackAsync();
                    return Result<bool>.Fail("Product not found Or is InActive", 404);
                }

                if (product.varint == null)
                {
                    await transaction.RollbackAsync();
                    return Result<bool>.Fail("No variant with this id or no quantity", 404);
                }

                var cart = await _cartRepository.GetCartByUserIdAsync(userId);
                if (cart == null)
                {
                    cart = await CreateNewCartAsync(userId);
                    if (cart == null)
                    {
                        await transaction.RollbackAsync();
                        return Result<bool>.Fail("Failed to create cart", 500);
                    }
                    else
                        await _unitOfWork.CommitAsync();
                }

                // Lock the cart row to serialize modifications
                await _unitOfWork.context.Database.ExecuteSqlRawAsync(
                    "SELECT Id FROM Cart WHERE Id = {0} FOR UPDATE",
                    cart.Id);

                if (itemDto.Quantity > product.varint.Quantity)
                {
                    await transaction.RollbackAsync();
                    return Result<bool>.Fail("Not enough quantity in stock for this variant", 400);
                }

                var existingItem = cart.Items.FirstOrDefault(i => i.ProductVariantId == itemDto.ProductVariantId);
                decimal finalPrice = product.Discount != null && product.Discount.IsActive && product.Discount.DeletedAt == null && product.Discount.EndDate > DateTime.UtcNow
                    ? Math.Round(product.Price - product.Discount.DiscountPercent / 100m * product.Price, 2)
                    : product.Price;

                if (existingItem != null)
                {
                    int totalRequestedQuantity = (existingItem?.Quantity ?? 0) + itemDto.Quantity;

                    if (totalRequestedQuantity > product.varint.Quantity)
                    {
                        await transaction.RollbackAsync();
                        return Result<bool>.Fail("Not enough quantity in stock for this variant", 400);
                    }
                    existingItem.Quantity = totalRequestedQuantity;
                    existingItem.ModifiedAt = DateTime.UtcNow;

                    var updateResult = _unitOfWork.Repository<CartItem>().Update(existingItem);
                    if (!updateResult)
                    {
                        await transaction.RollbackAsync();
                        return Result<bool>.Fail("Failed to update cart item", 500);
                    }
                }
                else
                {
                   

                    var cartItem = new CartItem
                    {
                        CartId = cart.Id,
                        ProductId = itemDto.ProductId,
                        ProductVariantId = itemDto.ProductVariantId,
                        Quantity = itemDto.Quantity,
                        AddedAt = DateTime.UtcNow,
                        UnitPrice = finalPrice
                    };

                    var addResult = await _unitOfWork.Repository<CartItem>().CreateAsync(cartItem);
                    if (addResult == null)
                    {
                        await transaction.RollbackAsync();
                        return Result<bool>.Fail("Failed to add cart item", 500);
                    }
                }

                var adminLog = await _userOpreationServices.AddUserOpreationAsync(
                    $"Added item to cart - Product: {itemDto.ProductId}, Quantity: {itemDto.Quantity}",
                    Opreations.UpdateOpreation,
                    userId,
                    cart.Id
                );

                if (!adminLog.Success)
                {
                    await transaction.RollbackAsync();
                    _logger.LogWarning($"Failed to log admin operation: {adminLog.Message}");
                    return Result<bool>.Fail("Failed to log admin operation", 500);
                }

                _backgroundJobClient.Enqueue(()=> RemoveCheckoutAsync(cart.Id));
				await _unitOfWork.CommitAsync();
                await transaction.CommitAsync();

                await _cacheHelper.RemoveCartCacheAsync(userId);

                _logger.LogInformation($"Item added to cart for user: {userId}, product: {itemDto.ProductId}");
                return Result<bool>.Ok(true, "Item added to cart successfully", 200);
            }
            catch (DbUpdateConcurrencyException e)
            {
                await transaction.RollbackAsync();
                _logger.LogWarning(e, "Concurrency conflict while adding item to cart for user {UserId}", userId);
                return Result<bool>.Fail("Cart was modified by another process.", 409);
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError($"Error adding item to cart for user {userId}: {ex.Message}");
                _cacheHelper.NotifyAdminError($"Error adding item to cart for user {userId}: {ex.Message}", ex.StackTrace);
                return Result<bool>.Fail("An error occurred while adding item to cart", 500);
            }
        }

        public async Task<Result<bool>> UpdateCartItemAsync(string userId, int productId, UpdateCartItemDto itemDto, int? productVariantId = null)
        {
            if (string.IsNullOrWhiteSpace(userId))
            {
                _logger.LogWarning("UpdateCartItemAsync called with empty userId");
                return Result<bool>.Fail("Invalid user ID", 400);
            }
            if (itemDto == null)
            {
                _logger.LogWarning("UpdateCartItemAsync called with null itemDto");
                return Result<bool>.Fail("Invalid item data", 400);
            }
            if (itemDto.Quantity <= 0)
            {
                _logger.LogWarning($"UpdateCartItemAsync called with non-positive quantity: {itemDto.Quantity}");
                return Result<bool>.Fail("Quantity must be greater than zero", 400);
            }
            if (productVariantId == null)
            {
                _logger.LogWarning("UpdateCartItemAsync called with null productVariantId");
                return Result<bool>.Fail("Product variant ID is required", 400);
            }

            _logger.LogInformation($"Updating cart item for user: {userId}, product: {productId}, variant: {productVariantId}");

            using var transaction = await _unitOfWork.BeginTransactionAsync();
            try
            {
                var cart = await _cartRepository.GetCartByUserIdAsync(userId);
                if (cart == null)
                {
                    _logger.LogWarning($"Cart not found for user: {userId}");
                    await transaction.RollbackAsync();
                    return Result<bool>.Fail("Cart not found", 404);
                }

                await _unitOfWork.context.Database.ExecuteSqlRawAsync(
                    "SELECT Id FROM Cart WHERE Id = {0} FOR UPDATE",
                    cart.Id);

                var cartItem = cart.Items.FirstOrDefault(i => i.ProductId == productId && i.ProductVariantId == productVariantId);
                if (cartItem == null)
                {
                    _logger.LogWarning($"Cart item not found for user: {userId}, product: {productId}, variant: {productVariantId}");
                    await transaction.RollbackAsync();
                    return Result<bool>.Fail("Cart item not found", 404);
                }

                if (cartItem.Quantity == itemDto.Quantity)
                {
                    await transaction.RollbackAsync();
                    _logger.LogInformation($"No update needed: cart item quantity is already {itemDto.Quantity} for user: {userId}, product: {productId}, variant: {productVariantId}");
                    return Result<bool>.Ok(true, "Cart item already has the requested quantity", 200);
                }

            

                var variant = await _unitOfWork.ProductVariant.GetAll()
                    .Where(v => v.Id == productVariantId && v.ProductId == productId && v.DeletedAt == null && v.IsActive&&v.Product.DeletedAt==null&&v.Product.IsActive)
                    .FirstOrDefaultAsync();

                if (variant == null)
                {
                    _logger.LogWarning($"Product variant not found or inactive: {productVariantId} for product: {productId}");
                    await transaction.RollbackAsync();
                    return Result<bool>.Fail("Product variant not found or inactive", 404);
                }

                if (variant.Quantity <= 0)
                {
                    _logger.LogWarning($"Insufficient or zero quantity for variant {productVariantId}. Requested: {itemDto.Quantity}, Available: {variant.Quantity}");
                    await transaction.RollbackAsync();
                    return Result<bool>.Fail($"Insufficient quantity. Available: {variant.Quantity}", 400);
                }
                if (itemDto.Quantity > variant.Quantity)
                {
                    _logger.LogWarning($"Requested quantity {itemDto.Quantity} exceeds available quantity {variant.Quantity} for variant {productVariantId}");
                    await transaction.RollbackAsync();
                    return Result<bool>.Fail($"Requested quantity exceeds available stock for this variant. Available: {variant.Quantity}", 400);
                }

                cartItem.Quantity = itemDto.Quantity;
                cartItem.ModifiedAt = DateTime.UtcNow;

                var updateResult = _unitOfWork.Repository<CartItem>().Update(cartItem);
                if (!updateResult)
                {
                    _logger.LogError($"Failed to update cart item for user: {userId}, product: {productId}, variant: {productVariantId}");
                    await transaction.RollbackAsync();
                    return Result<bool>.Fail("Failed to update cart item", 500);
                }

                var adminLog = await _userOpreationServices.AddUserOpreationAsync(
                    $"Updated cart item - Product: {productId}, Variant: {productVariantId}, Quantity: {itemDto.Quantity}",
                    Opreations.UpdateOpreation,
                    userId,
                    cart.Id
                );
                if (!adminLog.Success)
                {
                    _logger.LogWarning($"Failed to log admin operation: {adminLog.Message}");
                }



				await _unitOfWork.CommitAsync();
                await transaction.CommitAsync();
				_backgroundJobClient.Enqueue(() => RemoveCheckoutAsync(cart.Id));

                _=_cacheHelper.RemoveCartCacheAsync(userId);

                _logger.LogInformation($"Cart item updated successfully for user: {userId}, product: {productId}, variant: {productVariantId}");
                return Result<bool>.Ok(true, "Cart item updated successfully", 200);
            }
            catch (DbUpdateConcurrencyException e)
            {
                await transaction.RollbackAsync();
                _logger.LogWarning(e, "Concurrency conflict while updating cart item for user {UserId}", userId);
                return Result<bool>.Fail("Cart was modified by another process.", 409);
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, $"Error updating cart item for user {userId}, product {productId}, variant {productVariantId}");
                _cacheHelper.NotifyAdminError($"Error updating cart item for user {userId}, product {productId}, variant {productVariantId}: {ex.Message}", ex.StackTrace);
                return Result<bool>.Fail("An error occurred while updating cart item", 500);
            }
        }

        public async Task<Result<bool>> RemoveItemFromCartAsync(string userId, RemoveCartItemDto itemDto)
        {
            _logger.LogInformation($"Removing item from cart for user: {userId}, product: {itemDto.ProductId}");

            using var transaction = await _unitOfWork.BeginTransactionAsync();
            try
            {
                var cart = await _cartRepository.GetCartByUserIdAsync(userId);
                if (cart == null)
                {
                    await transaction.RollbackAsync();
                    return Result<bool>.Fail("Cart not found", 404);
                }

                await _unitOfWork.context.Database.ExecuteSqlRawAsync(
                    "SELECT Id FROM Cart WHERE Id = {0} FOR UPDATE",
                    cart.Id);

                var removeResult = await _cartRepository.RemoveCartItemAsync(cart.Id, itemDto.ProductId, itemDto.ProductVariantId);
                if (!removeResult)
                {
                    await transaction.RollbackAsync();
                    return Result<bool>.Fail("Cart item not found", 404);
                }

                var adminLog = await _userOpreationServices.AddUserOpreationAsync(
                    $"Removed item from cart - Product: {itemDto.ProductId}",
                    Opreations.UpdateOpreation,
                    userId,
                    cart.Id
                );

                if (!adminLog.Success)
                {
                    await transaction.RollbackAsync();
                    _logger.LogWarning($"Failed to log admin operation: {adminLog.Message}");
                    return Result<bool>.Fail("An error occurred while removing item from cart", 500);
                }


				await _unitOfWork.CommitAsync();
                await transaction.CommitAsync();

				_backgroundJobClient.Enqueue(() => RemoveCheckoutAsync(cart.Id));
                _=_cacheHelper.RemoveCartCacheAsync(userId);

                _logger.LogInformation($"Item removed from cart for user: {userId}, product: {itemDto.ProductId}");
                return Result<bool>.Ok(true, "Item removed from cart successfully", 200);
            }
            catch (DbUpdateConcurrencyException e)
            {
                await transaction.RollbackAsync();
                _logger.LogWarning(e, "Concurrency conflict while removing cart item for user {UserId}", userId);
                return Result<bool>.Fail("Cart was modified by another process.", 409);
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError($"Error removing item from cart for user {userId}: {ex.Message}");
                _cacheHelper.NotifyAdminError($"Error removing item from cart for user {userId}: {ex.Message}", ex.StackTrace);
                return Result<bool>.Fail("An error occurred while removing item from cart", 500);
            }
        }

        public async Task<Result<bool>> ClearCartAsync(string userId)
        {
            _logger.LogInformation($"Clearing cart for user: {userId}");

            using var transaction = await _unitOfWork.BeginTransactionAsync();
            try
            {
                var cart = await _cartRepository.GetCartByUserIdAsync(userId);
                if (cart == null)
                {
                    await transaction.RollbackAsync();
                    return Result<bool>.Fail("Cart not found", 404);
                }

                await _unitOfWork.context.Database.ExecuteSqlRawAsync(
                    "SELECT Id FROM Cart WHERE Id = {0} FOR UPDATE",
                    cart.Id);

                var clearResult = await _cartRepository.ClearCartAsync(cart.Id);
                if (!clearResult)
                {
                    await transaction.RollbackAsync();
                    return Result<bool>.Fail("Failed to clear cart", 500);
                }

                var adminLog = await _userOpreationServices.AddUserOpreationAsync(
                    "Cleared cart",
                    Opreations.UpdateOpreation,
                    userId,
                    cart.Id
                );

                if (!adminLog.Success)
                {
                    await transaction.RollbackAsync();
                    _logger.LogWarning($"Failed to log admin operation: {adminLog.Message}");
                    return Result<bool>.Fail("An error occurred while clearing cart", 500);
                }
				_backgroundJobClient.Enqueue(() => RemoveCheckoutAsync(cart.Id));

				await _unitOfWork.CommitAsync();
                await transaction.CommitAsync();
                await _cacheHelper.RemoveCartCacheAsync(userId);

                return Result<bool>.Ok(true, "Cart cleared successfully", 200);
            }
            catch (DbUpdateConcurrencyException e)
            {
                await transaction.RollbackAsync();
                _logger.LogWarning(e, "Concurrency conflict while clearing cart for user {UserId}", userId);
                return Result<bool>.Fail("Cart was modified by another process.", 409);
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError($"Error clearing cart for user {userId}: {ex.Message}");
                _cacheHelper.NotifyAdminError($"Error clearing cart for user {userId}: {ex.Message}", ex.StackTrace);
                return Result<bool>.Fail("An error occurred while clearing cart", 500);
            }
        }

        public async Task<Result<bool>> UpdateCheckoutData(string userId)
        {
            _logger.LogInformation($"Checkout of cart for user: {userId}");

            var cart = await _cartRepository.GetCartByUserIdAsync(userId);
            if (cart == null)
            {
                var newCart = await CreateNewCartAsync(userId);
                if (newCart == null)
                {
                    _logger.LogError("Failed to create new cart during checkout.");
                    return Result<bool>.Fail("Unexpected error while creating a new cart");
                }

                return Result<bool>.Fail("Cart is empty. Add items before checkout.");
            }

            cart.CheckoutDate = DateTime.UtcNow;

            try
            {
                await _unitOfWork.CommitAsync();

                _=_cacheHelper.RemoveCartCacheAsync(userId);

                return Result<bool>.Ok(true, "Checkout successful");
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error while updating cart for checkout: {ex.Message}");
                return Result<bool>.Fail("Error occurred during checkout. Try again later.");
            }
        }

        public async Task UpdateCartItemsForProductsAfterAddDiscountAsync(List<int> productIds, decimal discountPercent)
        {
            var cartItems = await _unitOfWork.Repository<CartItem>()
                .GetAll()
                .Where(ci => productIds.Contains(ci.ProductId))
                .Include(ci => ci.Product)
                .ToListAsync();
            HashSet<int> updatecartcheckout = new HashSet<int>();
			foreach (var item in cartItems)
            {
                var newPrice = item.Product.Price - item.Product.Price * discountPercent / 100;
                if (item.UnitPrice != newPrice)
                {
					updatecartcheckout.Add(item.CartId);
					item.UnitPrice = newPrice;
                    item.ModifiedAt = DateTime.UtcNow;
                }
            }
            foreach (var id in updatecartcheckout)
            {
              
                await RemoveCheckoutAsync(id);
			}

			_unitOfWork.Repository<CartItem>().UpdateList(cartItems);
            await _unitOfWork.CommitAsync();

            _cacheHelper.ClearCartCache();
        }

		public async Task UpdateCartItemsForProductsAfterRemoveDiscountAsync(List<int> productIds)
		{
			var cartItems = await _unitOfWork.Repository<CartItem>()
				.GetAll()
				.Where(ci => productIds.Contains(ci.ProductId))
				.Include(ci => ci.Product)
				.ToListAsync();

			HashSet<int> cartIds = new HashSet<int>();

			foreach (var item in cartItems)
			{
				item.UnitPrice = item.Product.Price;
				item.ModifiedAt = DateTime.UtcNow;
				cartIds.Add(item.CartId);
			}

			_unitOfWork.Repository<CartItem>().UpdateList(cartItems);


			foreach (var cartId in cartIds)
			{
				await ResetCheckoutStatusAndRecalculateAsync(cartId);
			}

			await _unitOfWork.CommitAsync();
			_cacheHelper.ClearCartCache();
		}

		public async Task ResetCheckoutStatusAndRecalculateAsync(int cartId)
		{
			var cart = await _cartRepository.GetByIdAsync(cartId); 
			if (cart == null)
				return;

			cart.CheckoutDate = null;


			cart.ModifiedAt = DateTime.UtcNow;

         await    _unitOfWork.CommitAsync();
        }


		public async Task RemoveCheckoutAsync(int id)
        {
            var cart = await _cartRepository.GetByIdAsync(id);
            if (cart == null)
                return;
            cart.CheckoutDate = null;
            await _unitOfWork.CommitAsync();
            _cacheHelper.ClearCartCache();
		}

		private async Task<Cart?> CreateNewCartAsync(string userId)
        {
            try
            {
                var customer = await _userManager.FindByIdAsync(userId);
                if (customer == null)
                {
                    _logger.LogWarning($"Customer not found for user: {userId}");
                    return null;
                }

                var cart = new Cart
                {
                    UserId = userId,
                    CustomerId = userId,
                    CreatedAt = DateTime.UtcNow
                };

                var createdCart = await _cartRepository.CreateAsync(cart);
                if (createdCart == null)
                {
                    _logger.LogError($"Failed to create cart for user: {userId}");
                    return null;
                }

                return createdCart;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error creating new cart for user {userId}: {ex.Message}");
                _cacheHelper.NotifyAdminError($"Error creating new cart for user {userId}: {ex.Message}", ex.StackTrace);
				return null;
            }
        }
    }
}
