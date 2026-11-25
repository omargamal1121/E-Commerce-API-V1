using Domain.Models;
using Infrastructure.Context;
using Infrastructure.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Repository
{
    public class PromoCodeRepository : MainRepository<PromoCode>, IPromoCodeRepository
    {
        private readonly AppDbContext _context;
        private readonly ILogger<PromoCodeRepository> _logger;

        public PromoCodeRepository(AppDbContext context, ILogger<PromoCodeRepository> logger)
            : base(context, logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<PromoCode?> GetByCodeAsync(string code, bool includeDeleted = false, bool asNoTracking = false)
        {
            var normalizedCode = code.Trim().ToUpperInvariant();
            IQueryable<PromoCode> query = _context.PromoCodes;

            if (asNoTracking)
            {
                query = query.AsNoTracking();
            }

            if (!includeDeleted)
            {
                query = query.Where(p => p.DeletedAt == null);
            }

            return await query.FirstOrDefaultAsync(p => p.Code == normalizedCode);
        }

        public async Task<bool> CodeExistsAsync(string code, int? excludeId = null)
        {
            var normalizedCode = code.Trim().ToUpperInvariant();
            IQueryable<PromoCode> query = _context.PromoCodes.AsNoTracking().Where(p => p.Code == normalizedCode);

            if (excludeId.HasValue)
            {
                query = query.Where(p => p.Id != excludeId.Value);
            }

            return await query.AnyAsync();
        }

        public async Task<int> GetOrderCountForPromoCodeAsync(int promoCodeId)
        {
            return await _context.Orders.AsNoTracking().CountAsync(o => o.PromoCodeId == promoCodeId);
        }

        public IQueryable<PromoCode> GetQueryable(bool includeDeleted = false, bool asNoTracking = false)
        {
            IQueryable<PromoCode> query = _context.PromoCodes;

            if (asNoTracking)
            {
                query = query.AsNoTracking();
            }

            if (!includeDeleted)
            {
                query = query.Where(p => p.DeletedAt == null);
            }

            return query;
        }
    }
}
