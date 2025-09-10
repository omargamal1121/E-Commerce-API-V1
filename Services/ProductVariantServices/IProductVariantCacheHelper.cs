using E_Commerce.DtoModels.ProductDtos;

namespace E_Commerce.Services.ProductVariantServices
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
