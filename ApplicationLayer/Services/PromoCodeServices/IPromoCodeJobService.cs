namespace Application.Services.PromoCodeServices
{
    public interface IPromoCodeJobService
    {
        Task ActivatePromoCodeJobAsync(int promoCodeId);
        Task DeactivatePromoCodeJobAsync(int promoCodeId);
    }
}
