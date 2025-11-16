using ApplicationLayer.Services.Cache;
using ApplicationLayer.Services.EmailServices;
using ApplicationLayer.Interfaces;

using ApplicationLayer.Services.SubCategoryServices;
using Hangfire;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ApplicationLayer.Services.ProductServices;
using ApplicationLayer.DtoModels.DiscoutDtos;
using ApplicationLayer.Services.CollectionServices;

namespace ApplicationLayer.Services.DiscountServices
{
    public class DiscountCacheHelper : IDiscountCacheHelper
    {
    
        private readonly ILogger<DiscountCacheHelper> _logger;
        private readonly ICollectionCacheHelper _collectionCacheHelper;
        private readonly ISubCategoryCacheHelper _subCategoryCacheHelper;
        private readonly IProductCacheManger _productCacheManger;
        private readonly string[] _discountTags = new[] { "discount" };
        private readonly ICacheManager _cacheManager;
        private IBackgroundJobClient _backgroundJobClient;





        public DiscountCacheHelper(
            IBackgroundJobClient backgroundJobClient,
            ICacheManager cacheManager,

            ILogger<DiscountCacheHelper> logger,
            IProductCacheManger productCacheManger,
            ICollectionCacheHelper collectionCacheHelper,
            ISubCategoryCacheHelper subCategoryCacheHelper)
        {
         
            _backgroundJobClient = backgroundJobClient;
            _cacheManager = cacheManager;
            _logger = logger;
            _collectionCacheHelper = collectionCacheHelper;
            _subCategoryCacheHelper = subCategoryCacheHelper;
            _productCacheManger = productCacheManger;
        }

        public void ClearProductCache()
        {
            // Clear product cache
            _productCacheManger.ClearProductCache();

            // Clear collection cache that stores products
            _collectionCacheHelper.ClearCollectionCache();
            
            // Clear subcategory cache that stores products
            _subCategoryCacheHelper.ClearSubCategoryCache();
        }
        private string GetKey(int? id=null,bool? isActive=null,bool? isDeleted=null,string?Searchkey=null,int?page=null,int?PageSize=null,bool? IsAdmin=false)
        {
            return $"discount:id:{id}:isActive:{isActive}:IsDeleted:{isDeleted}:SearchKey:{Searchkey}:Page:{page}:pageSize:{PageSize}:IsAdmin:{IsAdmin}";
        }
        public void SetCache(DiscountDto discountDto, int? id = null, bool? isActive = null, bool? isDeleted = null, bool? IsAdmin = false)
        {
            var key = GetKey(id,isActive,isDeleted,IsAdmin:IsAdmin);
            _logger.LogInformation($"Set with Key{key}");
            _backgroundJobClient.Enqueue(() => _cacheManager.SetAsync(key, discountDto, TimeSpan.FromHours(1), _discountTags));
        }
        public void SetCache(List< DiscountDto> discountDto,
           
            bool? isActive = null,
            bool? isDeleted = null,
            string? Searchkey = null,
            bool? IsAdmin = false,
            int? page = null, 
            int? PageSize = null)
        {
            var key = GetKey(null,isActive,isDeleted,Searchkey,IsAdmin:IsAdmin,page:page,PageSize:PageSize);
            _logger.LogInformation($"Set with Key{key}");
            _backgroundJobClient.Enqueue(() => _cacheManager.SetAsync(key, discountDto, TimeSpan.FromHours(1), _discountTags));
        }

       public async Task< DiscountDto?> GetCacheAsync(int? id = null, bool? isActive = null, bool? isDeleted = null, bool? IsAdmin = false)
        {
            var key = GetKey(id, isActive, isDeleted, IsAdmin: IsAdmin);
            _logger.LogInformation($"Get with Key{key}");
            return  await _cacheManager.GetAsync<DiscountDto>(key);
        }
       public async Task<List< DiscountDto>?> GetCacheAsync( bool? isActive = null, bool? isDeleted = null,string? SearchKey=null, bool? IsAdmin = false,int ? page = null, int? PageSize = null)
        {
            var key = GetKey(null, isActive, isDeleted,SearchKey, IsAdmin: IsAdmin, page: page, PageSize: PageSize);
            _logger.LogInformation($"Get with Key{key}");
            return  await _cacheManager.GetAsync<List<DiscountDto>>(key);
        }


    }
}


