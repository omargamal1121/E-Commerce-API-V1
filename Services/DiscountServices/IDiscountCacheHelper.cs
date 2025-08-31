namespace E_Commerce.Services.Discount
{
    public interface IDiscountCacheHelper
    {
        void ClearProductCache();
        void NotifyAdminError(string message, string? stackTrace = null);
        void ScheduleDiscountCheck(int discountId, DateTime startDate, DateTime endDate);
        Task CheckOnDiscount(int id);
    }
}
