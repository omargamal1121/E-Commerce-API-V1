using E_Commerce.DtoModels.ProductDtos;

namespace E_Commerce.Services.ProductServices
{
    public interface IProductVariantCacheHelper
    {
        string GetVariantCacheKey(int id);
        string GetProductVariantsCacheKey(int productId);
        string GetProductCacheTag(int productId);
        void RemoveProductCachesAsync();
        Task CacheVariantAsync(int id, ProductVariantDto variant);
        Task CacheProductVariantsAsync(int productId, List<ProductVariantDto> variants);
        Task CacheSearchResultsAsync(int productId, string cacheKey, List<ProductVariantDto> variants);
    }
}
