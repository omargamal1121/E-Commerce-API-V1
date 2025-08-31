using E_Commerce.Services.Cache;
using E_Commerce.Services.EmailServices;
using E_Commerce.Interfaces;
using E_Commerce.UOW;
using E_Commerce.Services.Collection;
using E_Commerce.Services.SubCategoryServices;
using Hangfire;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace E_Commerce.Services.Discount
{
    public class DiscountCacheHelper : IDiscountCacheHelper
    {
        private readonly IBackgroundJobClient _jobClient;
        private readonly ICacheManager _cacheManager;
        private readonly IUnitOfWork _unitOfWork;
        private readonly ICartServices _cartServices;
        private readonly ILogger<DiscountCacheHelper> _logger;
        private readonly ICollectionCacheHelper _collectionCacheHelper;
        private readonly ISubCategoryCacheHelper _subCategoryCacheHelper;

        private const string CACHE_TAG_PRODUCT_SEARCH = "product_search";
        private const string CACHE_TAG_CATEGORY_WITH_DATA = "categorywithdata";
        private const string PRODUCT_WITH_VARIANT_TAG = "productwithvariantdata";
        private static readonly string[] PRODUCT_CACHE_TAGS = new[] { CACHE_TAG_PRODUCT_SEARCH, CACHE_TAG_CATEGORY_WITH_DATA, PRODUCT_WITH_VARIANT_TAG };

        public DiscountCacheHelper(
            IBackgroundJobClient jobClient, 
            ICacheManager cacheManager,
            IUnitOfWork unitOfWork,
            ICartServices cartServices,
            ILogger<DiscountCacheHelper> logger,
            ICollectionCacheHelper collectionCacheHelper,
            ISubCategoryCacheHelper subCategoryCacheHelper)
        {
            _jobClient = jobClient;
            _cacheManager = cacheManager;
            _unitOfWork = unitOfWork;
            _cartServices = cartServices;
            _logger = logger;
            _collectionCacheHelper = collectionCacheHelper;
            _subCategoryCacheHelper = subCategoryCacheHelper;
        }

        public void ClearProductCache()
        {
            // Clear product cache
            _jobClient.Enqueue(() => _cacheManager.RemoveByTagsAsync(PRODUCT_CACHE_TAGS));
            
            // Clear collection cache that stores products
            _collectionCacheHelper.ClearCollectionCache();
            
            // Clear subcategory cache that stores products
            _subCategoryCacheHelper.ClearSubCategoryCache();
        }

        public void NotifyAdminError(string message, string? stackTrace = null)
        {
            _jobClient.Enqueue<IErrorNotificationService>(_ => _.SendErrorNotificationAsync(message, stackTrace));
        }

        public void ScheduleDiscountCheck(int discountId, DateTime startDate, DateTime endDate)
        {
            // Schedule the discount check using Hangfire's ability to call services directly
            _jobClient.Schedule<IDiscountCacheHelper>(service => service.CheckOnDiscount(discountId), startDate);
            _jobClient.Schedule<IDiscountCacheHelper>(service => service.CheckOnDiscount(discountId), endDate);
        }

        public async Task CheckOnDiscount(int id)
        {
            var discount = await _unitOfWork.Repository<Models.Discount>().GetByIdAsync(id);
            if (discount == null)
            {
                _logger.LogWarning($"Discount with ID {id} not found for check.");
                return;
            }
            
            bool shouldDeactivate = discount.IsActive &&
                        ((discount.EndDate <= DateTime.UtcNow) || discount.DeletedAt != null);

            bool shouldActivate = !discount.IsActive &&
                                  discount.EndDate >= DateTime.UtcNow &&
                                  discount.StartDate <= DateTime.UtcNow &&
                                  discount.DeletedAt == null;

            if (shouldDeactivate)
            {
                _logger.LogInformation($"Discount with ID {id} has expired. Deactivating it.");
                discount.IsActive = false;
                var productsids = await _unitOfWork.Product.GetAll().Where(p => p.DiscountId == id && p.DeletedAt == null).Select(p => p.Id).ToListAsync();
                
                _jobClient.Enqueue(() => _cartServices.UpdateCartItemsForProductsAfterRemoveDiscountAsync(productsids));
            }
            else if (shouldActivate)
            {
                _logger.LogInformation($"Discount with ID {id} is now valid. Activating it.");
                discount.IsActive = true;
                var productsids = await _unitOfWork.Product.GetAll().Where(p => p.DiscountId == id && p.DeletedAt == null).Select(p => p.Id).ToListAsync();
                
                _jobClient.Enqueue(() => _cartServices.UpdateCartItemsForProductsAfterAddDiscountAsync(productsids, discount.DiscountPercent));
            }

            if (shouldDeactivate || shouldActivate)
            {
                ClearProductCache();
                await _unitOfWork.CommitAsync();
            }
        }
    }
}
