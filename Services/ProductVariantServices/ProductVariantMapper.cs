using E_Commerce.DtoModels.ProductDtos;
using E_Commerce.Models;
using E_Commerce.Services.ProductVariantServices;

namespace E_Commerce.Services.ProductVariantServices
{
    public class ProductVariantMapper : IProductVariantMapper
    {
        public ProductVariantDto MapToProductVariantDto(ProductVariant variant)
        {
            if (variant == null)
                return null;

            return new ProductVariantDto
            {
                Id = variant.Id,
                Color = variant.Color,
                Size = variant.Size,
                Waist = variant.Waist,
                Length = variant.Length,
                Quantity = variant.Quantity,
                ProductId = variant.ProductId,
                IsActive = variant.IsActive,
                CreatedAt = variant.CreatedAt,
                DeletedAt = variant.DeletedAt,
                ModifiedAt = variant.ModifiedAt
            };
        }

        public List<ProductVariantDto> MapToProductVariantDtoList(List<ProductVariant> variants)
        {
            if (variants == null || !variants.Any())
                return new List<ProductVariantDto>();

            return variants.Select(MapToProductVariantDto).ToList();
        }

        public List<ProductVariantDto> MapToProductVariantDtoList(IEnumerable<ProductVariant> variants)
        {
            if (variants == null || !variants.Any())
                return new List<ProductVariantDto>();

            return variants.Select(MapToProductVariantDto).ToList();
        }

        public ProductVariant MapToProductVariant(CreateProductVariantDto dto)
        {
            if (dto == null)
                return null;

            return new ProductVariant
            {
                Color = dto.Color?.Trim(),
                Size = dto.Size,
                Waist = dto.Waist,
                Length = dto.Length,
                Quantity = dto.Quantity,
                IsActive = true
            };
        }

        public ProductVariant MapToProductVariant(UpdateProductVariantDto dto)
        {
            if (dto == null)
                return null;

            return new ProductVariant
            {
                Color = dto.Color?.Trim(),
                Size = dto.Size,
                Waist = dto.Waist,
                Length = dto.Length
            };
        }

        public IQueryable<ProductVariantDto> MapToProductVariantDtoQueryable(IQueryable<ProductVariant> query)
        {
            if (query == null)
                return Enumerable.Empty<ProductVariantDto>().AsQueryable();

            return query.Select(v => new ProductVariantDto
            {
                Id = v.Id,
                Color = v.Color,
                Size = v.Size,
                Waist = v.Waist,
                Length = v.Length,
                Quantity = v.Quantity,
                ProductId = v.ProductId,
                IsActive = v.IsActive,
                CreatedAt = v.CreatedAt,
                DeletedAt = v.DeletedAt,
                ModifiedAt = v.ModifiedAt
            });
        }
    }
}
