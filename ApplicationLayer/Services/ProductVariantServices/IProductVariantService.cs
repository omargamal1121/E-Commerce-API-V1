using ApplicationLayer.DtoModels.ProductDtos;
using DomainLayer.Enums;

namespace ApplicationLayer.Services.ProductVariantServices
{
	public interface IProductVariantService
    {
        Task<Result<List<ProductVariantDto>>> GetProductVariantsAsync(int productId);
        Task<Result<List<ProductVariantDto>>> GetProductVariantsAsync(int productId, bool? isActive, bool? deletedOnly);
        Task<Result<ProductVariantDto>> GetVariantByIdAsync(int id);
        Task<Result<List<ProductVariantDto>>> AddVariantsAsync(
        int productId,
        List<CreateProductVariantDto> dtos,
        string userId);

		Task<Result<ProductVariantDto>> AddVariantAsync(int productId, CreateProductVariantDto dto, string userId);
        Task<Result<ProductVariantDto>> UpdateVariantAsync(int id, UpdateProductVariantDto dto, string userId);
        Task<Result<bool>> DeleteVariantAsync(int id, string userId);

        public Task<Result<List<ProductVariantDto>>> GetVariantsBySearchAsync(int productId, string? color = null, int? Length = null, int? wist = null, VariantSize? size = null, bool? isActive = null, bool? deletedOnly = null);
        Task<Result<bool>> ActivateVariantAsync(int id, string userId);
        Task<Result<bool>> DeactivateVariantAsync(int id, string userId);
        Task<Result<bool>> AddVariantQuantityAsync(int id, int addQuantity, string userId);
        Task<Result<bool>> RemoveVariantQuantityAsync(int id, int removeQuantity, string userId);
        Task<Result<bool>> RestoreVariantAsync(int id, string userId);
        public Task<Result<bool>> RemoveQuntityAfterOrder(int id, int quantity);
        public Task<Result<bool>> AddQuntityAfterRestoreOrder(int id, int addQuantity);
    }
}

