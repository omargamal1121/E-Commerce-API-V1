using E_Commerce.DtoModels.CartDtos;
using E_Commerce.DtoModels.Responses;
using E_Commerce.Interfaces;

namespace E_Commerce.Services.CartServices
{
    public class CartServices : ICartServices
    {
        private readonly ICartCommandService _cartCommandService;
        private readonly ICartQueryService _cartQueryService;

        public CartServices(
            ICartCommandService cartCommandService,
            ICartQueryService cartQueryService)
        {
            _cartCommandService = cartCommandService ?? throw new ArgumentNullException(nameof(cartCommandService));
            _cartQueryService = cartQueryService ?? throw new ArgumentNullException(nameof(cartQueryService));
		}

		public async Task<Result<CartDto>> GetCartAsync(string userId)
        {
            return await _cartQueryService.GetCartAsync(userId);
        }

        public async Task<Result<bool>> AddItemToCartAsync(string userId, CreateCartItemDto itemDto)
        {
            return await _cartCommandService.AddItemToCartAsync(userId, itemDto);
        }

        public async Task<Result<bool>> UpdateCartItemAsync(string userId, int productId, UpdateCartItemDto itemDto, int? productVariantId = null)
        {
            return await _cartCommandService.UpdateCartItemAsync(userId, productId, itemDto, productVariantId);
        }

        public async Task<Result<bool>> RemoveItemFromCartAsync(string userId, RemoveCartItemDto itemDto)
        {
            return await _cartCommandService.RemoveItemFromCartAsync(userId, itemDto);
        }

        public async Task<Result<bool>> ClearCartAsync(string userId)
        {
            return await _cartCommandService.ClearCartAsync(userId);
        }

        public async Task<Result<int?>> GetCartItemCountAsync(string userId)
        {
            return await _cartQueryService.GetCartItemCountAsync(userId);
        }
      
        public async Task<Result<bool>> IsCartEmptyAsync(string userId)
        {
            return await _cartQueryService.IsCartEmptyAsync(userId);
        }
                
        public async Task<Result<bool>> UpdateCheckoutData(string userId)
            {
            return await _cartCommandService.UpdateCheckoutData(userId);
            }

		public async Task UpdateCartItemsForProductsAfterAddDiscountAsync(List<int> productIds, decimal discountPercent)
		{
            await _cartCommandService.UpdateCartItemsForProductsAfterAddDiscountAsync(productIds, discountPercent);
		}

		public async Task UpdateCartItemsForProductsAfterRemoveDiscountAsync(List<int> productIds)
		{
            await _cartCommandService.UpdateCartItemsForProductsAfterRemoveDiscountAsync(productIds);
        }
    }
} 