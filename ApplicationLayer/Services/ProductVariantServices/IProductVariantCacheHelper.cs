using Application.DtoModels.ProductDtos;

namespace Application.Services.ProductVariantServices
{
    public interface IProductVariantCacheHelper
    {
        string GetVariantCacheKey(int id);
        string GetProductVariantsCacheKey(int productId);
        string GetProductCacheTag(int productId);
        void RemoveProductCachesAsync();
        void CacheVariantAsync(int id, ProductVariantDto variant);
        void CacheProductVariantsAsync(int productId, List<ProductVariantDto> variants);
        void CacheSearchResultsAsync(int productId, string cacheKey, List<ProductVariantDto> variants);
    }
}


