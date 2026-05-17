using Application.DtoModels.CartDtos;
using Application.Interfaces;
using Domain.Models;
using Hangfire;
using Infrastructure.Interfaces;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Application.Services.CartServices
{
    public class CartQueryService : ICartQueryService
    {
        private readonly ILogger<CartQueryService> _logger;
        private readonly IBackgroundJobClient _backgroundJobClient;
        private readonly UserManager<Customer> _userManager;
        private readonly IUnitOfWork _unitOfWork;
        private readonly ICartRepository _cartRepository;
        private readonly ICartCacheHelper _cacheHelper;
        private readonly ICartMapper _mapper;

        public CartQueryService(
            ILogger<CartQueryService> logger,
            IBackgroundJobClient backgroundJobClient,
            UserManager<Customer> userManager,
            IUnitOfWork unitOfWork,
            ICartRepository cartRepository,
            ICartCacheHelper cacheHelper,
            ICartMapper mapper)
        {
            _logger = logger;
            _backgroundJobClient = backgroundJobClient;
            _userManager = userManager;
            _unitOfWork = unitOfWork;
            _cartRepository = cartRepository;
            _cacheHelper = cacheHelper;
            _mapper = mapper;
        }

        public async Task<Result<CartDto>> GetCartAsync(string userId)
        {
            _logger.LogInformation($"Getting cart for user: {userId}");

            //var cached = await _cacheHelper.GetCartCacheAsync<CartDto>(userId);
            //if (cached != null)
            //{
            //    _logger.LogInformation($"Cache hit for cart user: {userId}");
            //    return Result<CartDto>.Ok(cached, "Cart retrieved from cache", 200);
            //}

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
                    return Result<CartDto>.Ok(new CartDto { CreatedAt = newcart.CreatedAt, Id = newcart.Id, UserId = userId }, "New cart created successfully", 201);
                }
                else
                {
                    var cart = await _cartRepository.GetAll().Where(c => c.UserId == userId && c.DeletedAt == null).Select(_mapper.CartDtoSelector).FirstOrDefaultAsync();
                    decimal TotalCurrentPrice = 0m, TotalOriginalPrice = 0m;

					if (cart == null)
                    {
                        _logger.LogWarning($"Cart disappeared after existence check for user: {userId}");
                        return Result<CartDto>.Fail("Cart not found", 404);
                    }
				
					if (cart.Items == null || !cart.Items.Any())
					{
						cart.TotalCurrentPrice = 0;
						cart.TotalPriceAtAddTime = 0;
						cart.HasPriceChanges = false;

						return Result<CartDto>.Ok(cart, "Cart retrieved successfully", 200);
					}
					foreach (var item in cart.Items)
                        {
						    EnrichCartItem(item);
                         TotalCurrentPrice += item.CurrentPrice * item.Quantity;
                         TotalOriginalPrice += item.PriceAtAddTime * item.Quantity;
					
					}

                
                    cart.TotalCurrentPrice = TotalCurrentPrice;
                    cart.TotalPriceAtAddTime = TotalOriginalPrice;
                    cart.HasPriceChanges = TotalCurrentPrice != TotalOriginalPrice;

					return Result<CartDto>.Ok(cart, "Cart retrieved successfully", 200);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error getting cart for user {userId}: {ex.Message}");
                _cacheHelper.NotifyAdminError($"Error getting cart for user {userId}: {ex.Message}", ex.StackTrace);
                return Result<CartDto>.Fail("An error occurred while retrieving cart", 500);
            }
        }
		private void EnrichCartItem(CartItemDto item)
		{
			var finalPrice = item.Product.FinalPrice;

			item.CurrentPrice = finalPrice;
			item.IsPriceChanged = item.PriceAtAddTime != finalPrice;
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

        public async Task RemoveCheckout(string userid)
        {
            var cart = await _cartRepository.GetCartByUserIdAsync(userid);
            if (cart != null && cart.CheckoutDate != null)
            {
                cart.CheckoutDate = null;
                await _unitOfWork.CommitAsync();
                _logger.LogInformation($"Checkout date removed for cart of user: {userid}");
            }
        }

        private async Task RemoveCheckout(int id)
        {
            var cart = await _cartRepository.GetByIdAsync(id);
            if (cart != null && cart.CheckoutDate != null)
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

            if (cartItem != null && cartItem.UnitPrice != newPrice)
            {
                cartItem.UnitPrice = newPrice;
                _logger.LogInformation($"CartItem {cartItemId} price updated to {newPrice}");
            }
            await _unitOfWork.CommitAsync();
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


