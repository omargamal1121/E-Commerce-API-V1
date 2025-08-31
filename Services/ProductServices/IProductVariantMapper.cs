using E_Commerce.DtoModels.ProductDtos;
using E_Commerce.Models;

namespace E_Commerce.Services.ProductServices
{
    public interface IProductVariantMapper
    {
        ProductVariantDto MapToProductVariantDto(ProductVariant variant);
        List<ProductVariantDto> MapToProductVariantDtoList(List<ProductVariant> variants);
        List<ProductVariantDto> MapToProductVariantDtoList(IEnumerable<ProductVariant> variants);
        ProductVariant MapToProductVariant(CreateProductVariantDto dto);
        ProductVariant MapToProductVariant(UpdateProductVariantDto dto);
        IQueryable<ProductVariantDto> MapToProductVariantDtoQueryable(IQueryable<ProductVariant> query);
    }
}
