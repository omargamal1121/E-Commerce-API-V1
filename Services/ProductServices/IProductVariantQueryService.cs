using E_Commerce.DtoModels.ProductDtos;
using E_Commerce.DtoModels.Responses;
using E_Commerce.Enums;

namespace E_Commerce.Services.ProductServices
{
    public interface IProductVariantQueryService
    {
        Task<Result<List<ProductVariantDto>>> GetProductVariantsAsync(int productId);
        Task<Result<List<ProductVariantDto>>> GetProductVariantsAsync(int productId, bool? isActive, bool? deletedOnly);
        Task<Result<ProductVariantDto>> GetVariantByIdAsync(int id);
        Task<Result<List<ProductVariantDto>>> GetVariantsBySearchAsync(int productId, string? color = null, int? Length = null, int? wist = null, VariantSize? size = null, bool? isActive = null, bool? deletedOnly = null);
    }
}
