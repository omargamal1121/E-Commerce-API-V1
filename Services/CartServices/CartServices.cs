using AutoMapper;
using E_Commerce.DtoModels.CartDtos;
using E_Commerce.DtoModels.Responses;
using E_Commerce.Enums;
using E_Commerce.ErrorHnadling;
using E_Commerce.Interfaces;
using E_Commerce.Models;
using E_Commerce.Services.AdminOpreationServices;
using E_Commerce.Services.Cache;
using E_Commerce.Services.EmailServices;
using E_Commerce.UOW;
using Hangfire;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Linq;
using System.Linq.Expressions;

namespace E_Commerce.Services.CartServices
{
    public class CartServices : ICartServices
    {
        private readonly ILogger<CartServices> _logger;
   
        private readonly IBackgroundJobClient _backgroundJobClient;
		private readonly IErrorNotificationService _errorNotificationService;
        private readonly UserManager<Customer>_userManager;
		private readonly IMapper _mapper;
        private readonly IUnitOfWork _unitOfWork;
        private readonly ICartRepository _cartRepository;
        private readonly IAdminOpreationServices _adminOperationServices;
        private readonly ICacheManager _cacheManager;
        private const string CACHE_TAG_CART = "cart";

        public CartServices(
  
            IBackgroundJobClient backgroundJobClient,
			UserManager<Customer> userManager,
            IErrorNotificationService errorNotificationService,
			ILogger<CartServices> logger,
            IMapper mapper,
            IUnitOfWork unitOfWork,
            ICartRepository cartRepository,
            IAdminOpreationServices adminOperationServices,
            ICacheManager cacheManager)
        {
            _backgroundJobClient = backgroundJobClient;
            _userManager = userManager;
            _errorNotificationService = errorNotificationService;
			_logger = logger;
            _mapper = mapper;
            _unitOfWork = unitOfWork;
            _cartRepository = cartRepository;
            _adminOperationServices = adminOperationServices;
            _cacheManager = cacheManager;
        }

        private void NotifyAdminOfError(string message, string? stackTrace = null)
        {
            BackgroundJob.Enqueue<IErrorNotificationService>(_ => _.SendErrorNotificationAsync(message, stackTrace));
        }
		public static Expression<Func<Cart, CartDto>> CartDtoSelector =>
	cart => new CartDto
	{
		Id = cart.Id,
		UserId = cart.UserId,
		TotalItems = cart.Items.Count,
        CheckoutDate = cart.CheckoutDate,
        CreatedAt = cart.CreatedAt,

		Items = cart.Items.Select(item => new CartItemDto
		{
			Id = item.Id,
			ProductId = item.ProductId,
			Quantity = item.Quantity,
			AddedAt = item.AddedAt,
            
            
            
			Product = new DtoModels.ProductDtos.ProductForCartDto
			{
				Id = item.Product.Id,
				Name = item.Product.Name,
				Price = item.Product.Price, 
                
				FinalPrice =
					item.Product.Discount != null
					 && item.Product.Discount.IsActive
					 && item.Product.Discount.DeletedAt == null
					 && item.Product.Discount.EndDate > DateTime.UtcNow
						? item.Product.Price - item.Product.Discount.DiscountPercent / 100 * item.Product.Price
						: item.Product.Price,
				DiscountName =
					item.Product.Discount != null
					 && item.Product.Discount.IsActive
					 && item.Product.Discount.DeletedAt == null
					 && item.Product.Discount.EndDate > DateTime.UtcNow
						? item.Product.Discount.Name
						: null,
				DiscountPrecentage =
					item.Product.Discount != null
					 && item.Product.Discount.IsActive
					 && item.Product.Discount.DeletedAt == null
					 && item.Product.Discount.EndDate > DateTime.UtcNow
						? item.Product.Discount.DiscountPercent
						: 0,
				MainImageUrl = item.Product.Images
					.Where(img => img.IsMain && img.DeletedAt == null)
					.Select(img => img.Url)
					.FirstOrDefault()?? item.Product.Images
					.Where(img => img.DeletedAt == null)
					.Select(img => img.Url)
					.FirstOrDefault(),
				IsActive = item.Product.IsActive,

				productVariantForCartDto = new DtoModels.ProductDtos.ProductVariantForCartDto
				{
					Color = item.ProductVariant.Color,
                    Id= item.Product.Id,
                    CreatedAt = item.ProductVariant.CreatedAt,
                    DeletedAt= item.ProductVariant.DeletedAt,
                    ModifiedAt =item.ProductVariant.ModifiedAt,
					Size = item.ProductVariant.Size,
					Waist = item.ProductVariant.Waist,
					Length = item.ProductVariant.Length,
					Quantity = item.ProductVariant.Quantity
				}
			},
            UnitPrice=item.UnitPrice,
            
			
		}).ToList()
	};

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

				// Clear cache
				await _cacheManager.RemoveByTagAsync(CACHE_TAG_CART);

				return Result<bool>.Ok(true, "Checkout successful");
			}
			catch (Exception ex)
			{
				_logger.LogError($"Error while updating cart for checkout: {ex.Message}");
				return Result<bool>.Fail("Error occurred during checkout. Try again later.");
			}
		}

		public async Task<Result<CartDto>> GetCartAsync(string userId)
        {
            _logger.LogInformation($"Getting cart for user: {userId}");

            var cacheKey = $"{CACHE_TAG_CART}_user_{userId}";
            var cached = await _cacheManager.GetAsync<CartDto>(cacheKey);
            if (cached != null)
            {
                _logger.LogInformation($"Cache hit for cart user: {userId}");
                return Result<CartDto>.Ok(cached, "Cart retrieved from cache", 200);
            }

            try
            {
                var isexist = await _cartRepository.IsExsistByUserId(userId);
				if (!isexist)
                {
               
                    var newcart = await CreateNewCartAsync(userId);
                    if (newcart == null)
                    {
						return Result<CartDto>.Fail("Unexpected error while creating a new cart", 500);

					}
                    await _unitOfWork.CommitAsync();
					return Result<CartDto>.Ok( new CartDto { CreatedAt=newcart.CreatedAt,Id=newcart.Id,UserId=userId}, "New cart created successfully", 201);
				}
                else{
                     var cart = await _cartRepository.GetAll().Where(c=>c.UserId==userId&&c.DeletedAt==null).Select(CartDtoSelector).FirstOrDefaultAsync();
					if (cart == null)
					{
						_logger.LogWarning($"Cart disappeared after existence check for user: {userId}");
						return Result<CartDto>.Fail("Cart not found", 404);
					}
					cart.TotalPrice = cart.Items.Sum(i => i.Quantity * i.Product.FinalPrice);
					var oldTotal = cart.Items.Sum(i => i.Quantity * i.UnitPrice);

					if (Math.Round(cart.TotalPrice, 2) != Math.Round(oldTotal, 2))
					{
						foreach (var item in cart.Items)
						{
							var finalPrice = item.Product.FinalPrice;

							if (item.UnitPrice != finalPrice)
							{
								item.UnitPrice = finalPrice;
								_backgroundJobClient.Enqueue(() => UpdateCartItemPriceAsync(item.Id, finalPrice));
							}
						}

						_backgroundJobClient.Enqueue(() => RemoveCheckout(userId));
					}


					await _cacheManager.SetAsync(cacheKey, cart, tags: new[] { CACHE_TAG_CART });

                    return Result<CartDto>.Ok(cart, "Cart retrieved successfully", 200);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error getting cart for user {userId}: {ex.Message}");
                NotifyAdminOfError($"Error getting cart for user {userId}: {ex.Message}", ex.StackTrace);
                return Result<CartDto>.Fail("An error occurred while retrieving cart", 500);
            }
        }
        private async Task RemoveCheckout(string userid)
        {
            var cart= await _cartRepository.GetCartByUserIdAsync(userid);
            if(cart != null&&cart.CheckoutDate!=null)
            {
                cart.CheckoutDate = null;
                await _unitOfWork.CommitAsync();
                _logger.LogInformation($"Checkout date removed for cart of user: {userid}");
            }
		}
        private async Task RemoveCheckout(int id)
        {
            var cart= await _cartRepository.GetByIdAsync(id);
            if(cart != null&&cart.CheckoutDate!=null)
            {
                cart.CheckoutDate = null;
                await _unitOfWork.CommitAsync();
                _logger.LogInformation($"Checkout date removed for cart o: {id}");
            }
		}

		public async Task UpdateCartItemPriceAsync(int cartItemId, decimal newPrice)
		{


			var cartItem = await _unitOfWork.Repository<CartItem>()
				.GetAll()
				.FirstOrDefaultAsync(ci => ci.Id == cartItemId && ci.DeletedAt == null);



			if (cartItem != null&&cartItem.UnitPrice!=newPrice)
			{
				cartItem.UnitPrice = newPrice;

				_logger.LogInformation($"CartItem {cartItemId} price updated to {newPrice}");
			}
				await _unitOfWork.CommitAsync();
		}


		/// <summary>
		/// Updates the quantity of a cart item for a user, with full validation and transactional safety.
		/// Skips update if the quantity is unchanged. Returns only success/failure.
		/// </summary>
		public async Task<Result<bool>> UpdateCartItemAsync(string userId, int productId, UpdateCartItemDto itemDto, int? productVariantId = null)
        {
            // Parameter validation
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

   
                var cartItem = cart.Items.FirstOrDefault(i => i.ProductId == productId && i.ProductVariantId == productVariantId);
                if (cartItem == null)
                {
                    _logger.LogWarning($"Cart item not found for user: {userId}, product: {productId}, variant: {productVariantId}");
                    await transaction.RollbackAsync();
                    return Result<bool>.Fail("Cart item not found", 404);
                }

                // Idempotency: skip update if quantity is unchanged
                if (cartItem.Quantity == itemDto.Quantity)
                {
					await transaction.RollbackAsync();
					_logger.LogInformation($"No update needed: cart item quantity is already {itemDto.Quantity} for user: {userId}, product: {productId}, variant: {productVariantId}");
                    return Result<bool>.Ok(true, "Cart item already has the requested quantity", 200);
                }

                // Validate product and variant
                var product = await _unitOfWork.Product.GetAll()
                    .Where(p => p.Id == productId && p.DeletedAt == null && p.IsActive)
                    .FirstOrDefaultAsync();

                if (product == null)
                {
                    _logger.LogWarning($"Product not found or inactive: {productId}");
                    await transaction.RollbackAsync();
                    return Result<bool>.Fail("Product not found or inactive", 404);
                }

                // Fetch only the needed variant for performance
                var variant = await _unitOfWork.ProductVariant.GetAll()
                    .Where(v => v.Id == productVariantId && v.ProductId == productId && v.DeletedAt == null && v.IsActive)
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
                if( itemDto.Quantity > variant.Quantity)
                {
                    _logger.LogWarning($"Requested quantity {itemDto.Quantity} exceeds available quantity {variant.Quantity} for variant {productVariantId}");
                    await transaction.RollbackAsync();
                    return Result<bool>.Fail($"Requested quantity exceeds available stock for this variant. Available: {variant.Quantity}", 400);
				}

				cartItem.Quantity = itemDto.Quantity;
                cartItem.ModifiedAt = DateTime.UtcNow;
                

                var updateResult =  _unitOfWork.Repository<CartItem>().Update(cartItem);
                if (!updateResult)
                {
                    _logger.LogError($"Failed to update cart item for user: {userId}, product: {productId}, variant: {productVariantId}");
                    await transaction.RollbackAsync();
                    return Result<bool>.Fail("Failed to update cart item", 500);
                }

           
                var adminLog = await _adminOperationServices.AddAdminOpreationAsync(
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

                // Clear cache
                await _cacheManager.RemoveByTagAsync(CACHE_TAG_CART);

                _logger.LogInformation($"Cart item updated successfully for user: {userId}, product: {productId}, variant: {productVariantId}");
                return Result<bool>.Ok(true, "Cart item updated successfully", 200);
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, $"Error updating cart item for user {userId}, product {productId}, variant {productVariantId}");
                NotifyAdminOfError($"Error updating cart item for user {userId}, product {productId}, variant {productVariantId}: {ex.Message}", ex.StackTrace);
                return Result<bool>.Fail("An error occurred while updating cart item", 500);
            }
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
                    .Select(p=>new {
                        p.Id,   
                        varint= p.ProductVariants.Where(p => p.Id == itemDto.ProductVariantId && p.IsActive).Select(v => new
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

                if (product.varint==null)
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
                if(itemDto.Quantity > product.varint.Quantity)
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
					existingItem.Quantity=totalRequestedQuantity;
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
					var hasValidDiscount = product.Discount != null &&
						  product.Discount.StartDate <= DateTime.UtcNow &&
						  product.Discount.EndDate > DateTime.UtcNow &&
						  product.Discount.DeletedAt == null;

					var unitPrice = hasValidDiscount
						? Math.Round(product.Price - product.Discount.DiscountPercent / 100m * product.Price, 2)
						: product.Price;

					var cartItem = new CartItem
					{
						CartId = cart.Id,
						ProductId = itemDto.ProductId,
						ProductVariantId = itemDto.ProductVariantId,
						Quantity = itemDto.Quantity,
						AddedAt = DateTime.UtcNow,
						UnitPrice = unitPrice
					};

					var addResult = await _unitOfWork.Repository<CartItem>().CreateAsync(cartItem);
                    if (addResult == null)
                    {
                        await transaction.RollbackAsync();
                        return Result<bool>.Fail("Failed to add cart item", 500);
                    }
                }

               
            

                var adminLog = await _adminOperationServices.AddAdminOpreationAsync(
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

                await _unitOfWork.CommitAsync();
                await transaction.CommitAsync();

                var cacheKey = $"{CACHE_TAG_CART}_user_{userId}";
                await _cacheManager.RemoveAsync(cacheKey);

                _logger.LogInformation($"Item added to cart for user: {userId}, product: {itemDto.ProductId}");
                return Result<bool>.Ok(true, "Item added to cart successfully", 200);
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError($"Error adding item to cart for user {userId}: {ex.Message}");
                NotifyAdminOfError($"Error adding item to cart for user {userId}: {ex.Message}", ex.StackTrace);
                return Result<bool>.Fail("An error occurred while adding item to cart", 500);
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

                var removeResult = await _cartRepository.RemoveCartItemAsync(cart.Id, itemDto.ProductId, itemDto.ProductVariantId);
                if (!removeResult)
                {
                    await transaction.RollbackAsync();
                    return Result<bool>.Fail("Cart item not found", 404);
                }

                // Log admin operation
                var adminLog = await _adminOperationServices.AddAdminOpreationAsync(
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

                // Clear cache
                await _cacheManager.RemoveByTagAsync(CACHE_TAG_CART);

                _logger.LogInformation($"Item removed from cart for user: {userId}, product: {itemDto.ProductId}");
                return Result<bool>.Ok(true, "Item removed from cart successfully", 200);
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError($"Error removing item from cart for user {userId}: {ex.Message}");
                NotifyAdminOfError($"Error removing item from cart for user {userId}: {ex.Message}", ex.StackTrace);
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

                var clearResult = await _cartRepository.ClearCartAsync(cart.Id);
                if (!clearResult)
                {
                    await transaction.RollbackAsync();
                    return Result<bool>.Fail("Failed to clear cart", 500);
                }

                // Log admin operation
                var adminLog = await _adminOperationServices.AddAdminOpreationAsync(
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

                await _unitOfWork.CommitAsync();
                await transaction.CommitAsync();
                await _cacheManager.RemoveByTagAsync(CACHE_TAG_CART);

                return Result<bool>.Ok(true, "Cart cleared successfully", 200);
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError($"Error clearing cart for user {userId}: {ex.Message}");
                NotifyAdminOfError($"Error clearing cart for user {userId}: {ex.Message}", ex.StackTrace);
                return Result<bool>.Fail("An error occurred while clearing cart", 500);
            }
        }

        public async Task<Result<int?>> GetCartItemCountAsync(string userId)
        {
            try
            {
                var count = await _cartRepository.GetCartItemCountAsync(userId);
                return Result<int?>.Ok(count, "Cart item count retrieved", 200);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error getting cart item count for user {userId}: {ex.Message}");
                return Result<int?>.Fail("An error occurred while getting cart item count", 500);
            }
        }

      
        public async Task<Result<bool>> IsCartEmptyAsync(string userId)
        {
            try
            {
                var cart = await _cartRepository.IsEmptyAsync(userId);
                
                return Result<bool>.Ok(cart, "Cart empty status retrieved", 200);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error checking if cart is empty for user {userId}: {ex.Message}");
                return Result<bool>.Fail("An error occurred while checking cart status", 500);
            }
        }
		public async Task UpdateCartItemsForProductsAfterAddDiscountAsync(List<int> productIds, decimal discountPercent)
		{
			var cartItems = await _unitOfWork.Repository<CartItem>()
	        .GetAll()
	        .Where(ci => productIds.Contains(ci.ProductId))
	        .Include(ci => ci.Product) 
        	.ToListAsync();

			foreach (var item in cartItems)
			{
				var newPrice = item.Product.Price - item.Product.Price * discountPercent / 100;
				if (item.UnitPrice != newPrice)
				{
					item.UnitPrice = newPrice;
					item.ModifiedAt = DateTime.UtcNow;
				}
			}

			_unitOfWork.Repository<CartItem>().UpdateList(cartItems);
			await _unitOfWork.CommitAsync();

			_ = _cacheManager.RemoveByTagAsync(CACHE_TAG_CART);
		}

		public async Task UpdateCartItemsForProductsAfterRemoveDiscountAsync(List<int> productIds)
		{
			var cartItems = await _unitOfWork.Repository<CartItem>()
	.GetAll()
	.Where(ci => productIds.Contains(ci.ProductId))
	.Include(ci => ci.Product) 
	.ToListAsync();

			foreach (var item in cartItems)
			{
			
			
					item.UnitPrice = item.Product.Price;
					item.ModifiedAt = DateTime.UtcNow;
			
			}

			_unitOfWork.Repository<CartItem>().UpdateList(cartItems);
			await _unitOfWork.CommitAsync();

			_ = _cacheManager.RemoveByTagAsync(CACHE_TAG_CART);
		}


		private decimal CalculateFinalPrice(decimal price,decimal discountprecnet)
        {
            return price-(price*(discountprecnet/100));
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
                return null;
            }
        }
    }
} 