using E_Commerce.DtoModels.ProductDtos;
using E_Commerce.DtoModels.Responses;

namespace E_Commerce.Services.ProductServices
{
    public interface IProductVariantCommandService
    {
        Task<Result<ProductVariantDto>> AddVariantAsync(int productId, CreateProductVariantDto dto, string userId);
        Task<Result<ProductVariantDto>> UpdateVariantAsync(int id, UpdateProductVariantDto dto, string userId);
        Task<Result<bool>> DeleteVariantAsync(int id, string userId);
        Task<Result<bool>> ActivateVariantAsync(int id, string userId);
        Task<Result<bool>> DeactivateVariantAsync(int id, string userId);
        Task<Result<bool>> AddVariantQuantityAsync(int id, int addQuantity, string userId);
        Task<Result<bool>> RemoveVariantQuantityAsync(int id, int removeQuantity, string userId);
        Task<Result<bool>> RestoreVariantAsync(int id, string userId);
        Task<Result<bool>> RemoveQuntityAfterOrder(int id, int quantity);
        Task<Result<bool>> AddQuntityAfterRestoreOrder(int id, int addQuantity);
    }
}
