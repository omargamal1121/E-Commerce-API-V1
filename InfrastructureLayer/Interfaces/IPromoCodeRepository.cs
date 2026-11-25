using Domain.Models;

namespace Infrastructure.Interfaces
{
    public interface IPromoCodeRepository : IRepository<PromoCode>
    {
        Task<PromoCode?> GetByCodeAsync(string code, bool includeDeleted = false, bool asNoTracking = false);
        Task<bool> CodeExistsAsync(string code, int? excludeId = null);
        Task<int> GetOrderCountForPromoCodeAsync(int promoCodeId);
        IQueryable<PromoCode> GetQueryable(bool includeDeleted = false, bool asNoTracking = false);
    }
}
