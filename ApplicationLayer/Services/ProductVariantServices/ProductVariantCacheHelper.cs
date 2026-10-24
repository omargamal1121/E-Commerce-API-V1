using Application.DtoModels.ProductDtos;
using Application.Services.CacheServices;
using Application.Services.ProductVariantServices;
using Hangfire;

namespace Application.Services.ProductVariantServices
{
    public class ProductVariantCacheHelper : IProductVariantCacheHelper
    {
        private readonly ICacheManager _cacheManager;


        private const string VARIANT_DATA_TAG = "variantdata";
        private const string VARIANT_LIST_TAG = "variantlist";
        private static readonly string[] PRODUCT_CACHE_TAGS = new[] {  VARIANT_LIST_TAG ,VARIANT_DATA_TAG };

        public ProductVariantCacheHelper(
            ICacheManager cacheManager)
        {
            _cacheManager = cacheManager;
        }

        public string GetVariantCacheKey(int id) => $"variant:{id}";
        
        public string GetProductVariantsCacheKey(int productId) => $"product:{productId}:variants";
        
        public string GetProductCacheTag(int productId) => $"product:{productId}";

        public async Task RemoveProductCachesAsync()
        {
            await _cacheManager.RemoveByTagsAsync(PRODUCT_CACHE_TAGS);
        }

        public  void CacheVariantAsync(int id, ProductVariantDto variant)
        {
            var cacheKey = GetVariantCacheKey(id);
            _ = _cacheManager.SetAsync(cacheKey, variant, null, new[] { GetProductCacheTag(variant.ProductId), VARIANT_DATA_TAG });
        }
   
		public void CacheProductVariantsAsync(int productId, List<ProductVariantDto> variants)
        {
            var cacheKey = GetProductVariantsCacheKey(productId);
            _ = _cacheManager.SetAsync(cacheKey, variants, null, new[] { GetProductCacheTag(productId), VARIANT_DATA_TAG });
        }

        public  void CacheSearchResultsAsync(int productId, string cacheKey, List<ProductVariantDto> variants)
        {
            _ = _cacheManager.SetAsync(cacheKey, variants, null, new[] { GetProductCacheTag(productId), VARIANT_DATA_TAG });
        }
    }
}


