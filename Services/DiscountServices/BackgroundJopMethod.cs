using E_Commerce.Interfaces;
using E_Commerce.Services.CartServices;
using E_Commerce.Services.EmailServices;
using E_Commerce.UOW;
using Hangfire;
using Microsoft.EntityFrameworkCore;

namespace E_Commerce.Services.DiscountServices
{
	public interface IDiscountBackgroundJopMethod
	{
        void NotifyAdminError(string message, string? stackTrace = null);
        void ScheduleDiscountCheck(int discountId, DateTime startDate, DateTime endDate);
        Task CheckOnDiscount(int id);


    }

    public class DiscountBackgroundJopMethod : IDiscountBackgroundJopMethod
    {
        public readonly IUnitOfWork _unitOfWork;
        public readonly IBackgroundJobClient _jobClient;
        public readonly ICartCacheHelper _cartCacheHelper;
        public readonly ILogger<DiscountBackgroundJopMethod> _logger;
        public readonly ICartServices _cartServices;
		public DiscountBackgroundJopMethod(IUnitOfWork unitOfWork,
            IBackgroundJobClient backgroundJobClient, 
            ICartCacheHelper cartCacheHelper
            ,ILogger<DiscountBackgroundJopMethod> logger,
            ICartServices cartServices)
		{
            _unitOfWork = unitOfWork;
            _jobClient = backgroundJobClient;
            _cartCacheHelper = cartCacheHelper;
            _logger = logger;
            _cartServices = cartServices;

        }
		public void NotifyAdminError(string message, string? stackTrace = null)
        {
            _jobClient.Enqueue<IErrorNotificationService>(_ => _.SendErrorNotificationAsync(message, stackTrace));
        }

        public void ScheduleDiscountCheck(int discountId, DateTime startDate, DateTime endDate)
        {
            // Schedule the discount check using Hangfire's ability to call services directly
            _jobClient.Schedule<IDiscountCacheHelper>(_ => CheckOnDiscount(discountId), startDate);
            _jobClient.Schedule<IDiscountCacheHelper>(_ => CheckOnDiscount(discountId), endDate);
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
                var productsids = await _unitOfWork.Product.GetAll().AsNoTracking().Where(p => p.DiscountId == id && p.DeletedAt == null).Select(p => p.Id).ToListAsync();

                _jobClient.Enqueue(() => _cartServices.UpdateCartItemsForProductsAfterRemoveDiscountAsync(productsids));
            }
            else if (shouldActivate)
            {
                _logger.LogInformation($"Discount with ID {id} is now valid. Activating it.");
                discount.IsActive = true;
                var productsids = await _unitOfWork.Product.GetAll().AsNoTracking().Where(p => p.DiscountId == id && p.DeletedAt == null).Select(p => p.Id).ToListAsync();

                _jobClient.Enqueue(() => _cartServices.UpdateCartItemsForProductsAfterAddDiscountAsync(productsids, discount.DiscountPercent));
            }

            if (shouldDeactivate || shouldActivate)
            {
                 _cartCacheHelper.ClearCartCache();
                await _unitOfWork.CommitAsync();
            }
        }
    }
}
