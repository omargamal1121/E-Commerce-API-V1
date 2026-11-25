using Infrastructure.Interfaces;
using Microsoft.Extensions.Logging;

namespace Application.Services.PromoCodeServices
{
    public class PromoCodeJobService : IPromoCodeJobService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly ILogger<PromoCodeJobService> _logger;

        public PromoCodeJobService(IUnitOfWork unitOfWork, ILogger<PromoCodeJobService> logger)
        {
            _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task ActivatePromoCodeJobAsync(int promoCodeId)
        {
            _logger.LogInformation("Executing Hangfire job to activate promo code ID: {Id}", promoCodeId);

            var promoCode = await _unitOfWork.PromoCode.GetByIdAsync(promoCodeId);
            if (promoCode == null || promoCode.DeletedAt != null)
            {
                _logger.LogWarning("Promo code ID {Id} not found or deleted. Activation skipped.", promoCodeId);
                return;
            }

            var now = DateTime.UtcNow;
            if (promoCode.EndsAt <= now)
            {
                _logger.LogWarning("Promo code {Code} (ID: {Id}) already expired at {EndsAt}. Activation skipped.", promoCode.Code, promoCodeId, promoCode.EndsAt);
                return;
            }

            if (!promoCode.IsActive)
            {
                promoCode.IsActive = true;
                promoCode.ModifiedAt = now;
                _unitOfWork.PromoCode.Update(promoCode);
                await _unitOfWork.CommitAsync();
                _logger.LogInformation("Promo code {Code} (ID: {Id}) successfully activated by Hangfire job.", promoCode.Code, promoCodeId);
            }
        }

        public async Task DeactivatePromoCodeJobAsync(int promoCodeId)
        {
            _logger.LogInformation("Executing Hangfire job to deactivate promo code ID: {Id}", promoCodeId);

            var promoCode = await _unitOfWork.PromoCode.GetByIdAsync(promoCodeId);
            if (promoCode == null || promoCode.DeletedAt != null)
            {
                _logger.LogWarning("Promo code ID {Id} not found or deleted. Deactivation skipped.", promoCodeId);
                return;
            }

            if (promoCode.IsActive)
            {
                promoCode.IsActive = false;
                promoCode.ModifiedAt = DateTime.UtcNow;
                _unitOfWork.PromoCode.Update(promoCode);
                await _unitOfWork.CommitAsync();
                _logger.LogInformation("Promo code {Code} (ID: {Id}) successfully deactivated by Hangfire job.", promoCode.Code, promoCodeId);
            }
        }
    }
}
