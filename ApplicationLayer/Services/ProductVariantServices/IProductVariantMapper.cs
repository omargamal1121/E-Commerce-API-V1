using Application.DtoModels.ProductDtos;
using Domain.Models;

namespace Application.Services.ProductVariantServices
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


