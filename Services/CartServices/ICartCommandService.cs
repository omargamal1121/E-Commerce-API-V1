using E_Commerce.DtoModels.CartDtos;
using E_Commerce.DtoModels.Responses;

namespace E_Commerce.Services.CartServices
{
    public interface ICartCommandService
    {
        Task<Result<bool>> AddItemToCartAsync(string userId, CreateCartItemDto itemDto);
        Task<Result<bool>> UpdateCartItemAsync(string userId, int productId, UpdateCartItemDto itemDto, int? productVariantId = null);
        Task<Result<bool>> RemoveItemFromCartAsync(string userId, RemoveCartItemDto itemDto);
        Task<Result<bool>> ClearCartAsync(string userId);
        Task<Result<bool>> UpdateCheckoutData(string userId);
        Task UpdateCartItemsForProductsAfterAddDiscountAsync(List<int> productIds, decimal discountPercent);
        Task UpdateCartItemsForProductsAfterRemoveDiscountAsync(List<int> productIds);
    }
}
