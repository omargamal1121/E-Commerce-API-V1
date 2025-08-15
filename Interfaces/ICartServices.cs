using E_Commerce.DtoModels.CartDtos;
using E_Commerce.DtoModels.Responses;
using E_Commerce.Services;

namespace E_Commerce.Interfaces
{
    public interface ICartServices
    {
        Task<Result<CartDto>> GetCartAsync(string userId);
        public  Task UpdateCartItemsForProductsAfterAddDiscountAsync(List<int> productIds, decimal discountPercent);
        public  Task UpdateCartItemsForProductsAfterRemoveDiscountAsync(List<int> productIds);

		Task<Result<bool>> AddItemToCartAsync(string userId, CreateCartItemDto itemDto);
        Task<Result<bool>> UpdateCartItemAsync(string userId, int productId, UpdateCartItemDto itemDto, int? productVariantId = null);
        Task<Result<bool>> RemoveItemFromCartAsync(string userId, RemoveCartItemDto itemDto);
        Task<Result<bool>> ClearCartAsync(string userId);
        Task<Result<int?>> GetCartItemCountAsync(string userId);
        Task<Result<bool>> IsCartEmptyAsync(string userId);
        Task<Result<bool>> UpdateCheckoutData(string userId);

	}
} 