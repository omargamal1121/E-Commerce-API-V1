using E_Commerce.DtoModels.ProductDtos;
using E_Commerce.Services.Cache;
using E_Commerce.Services.ProductVariantServices;
using Hangfire;

namespace E_Commerce.Services.ProductVariantServices
{
    public class ProductVariantCacheHelper : IProductVariantCacheHelper
    {
        private readonly ICacheManager _cacheManager;
        private readonly IBackgroundJobClient _backgroundJobClient;


        private const string VARIANT_DATA_TAG = "variantdata";
        private const string VARIANT_LIST_TAG = "variantlist";
        private static readonly string[] PRODUCT_CACHE_TAGS = new[] {  VARIANT_LIST_TAG ,VARIANT_DATA_TAG };

        public ProductVariantCacheHelper(
            ICacheManager cacheManager,
            IBackgroundJobClient backgroundJobClient)
        {
            _cacheManager = cacheManager;
            _backgroundJobClient = backgroundJobClient;
        }

        public string GetVariantCacheKey(int id) => $"variant:{id}";
        
        public string GetProductVariantsCacheKey(int productId) => $"product:{productId}:variants";
        
        public string GetProductCacheTag(int productId) => $"product:{productId}";

        public void RemoveProductCachesAsync()
        {
            _backgroundJobClient.Enqueue(() => _cacheManager.RemoveByTagsAsync(PRODUCT_CACHE_TAGS));
        }

        public  void CacheVariantAsync(int id, ProductVariantDto variant)
        {
            var cacheKey = GetVariantCacheKey(id);
            _backgroundJobClient.Enqueue(() => _cacheManager.SetAsync(cacheKey, variant, null, new[] { GetProductCacheTag(variant.ProductId), VARIANT_DATA_TAG }));
        }
   
		public void CacheProductVariantsAsync(int productId, List<ProductVariantDto> variants)
        {
            var cacheKey = GetProductVariantsCacheKey(productId);
            _backgroundJobClient.Enqueue(() => _cacheManager.SetAsync(cacheKey, variants, null, new[] { GetProductCacheTag(productId), VARIANT_DATA_TAG }));
        }

        public  void CacheSearchResultsAsync(int productId, string cacheKey, List<ProductVariantDto> variants)
        {
            _backgroundJobClient.Enqueue(() => _cacheManager.SetAsync(cacheKey, variants, null, new[] { GetProductCacheTag(productId), VARIANT_DATA_TAG }));
        }
    }
}
