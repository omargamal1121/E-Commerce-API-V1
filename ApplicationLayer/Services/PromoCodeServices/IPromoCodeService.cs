using Application.DtoModels.PromoCodeDtos;

namespace Application.Services.PromoCodeServices
{
    public interface IPromoCodeService
    {
        Task<PromoCodeValidationResult> ValidatePromoCodeAsync(string? code, CancellationToken cancellationToken = default);
        Task<Result<PromoCodeValidationResultDto>> ValidatePublicAsync(string? code, CancellationToken cancellationToken = default);
        Task<Result<PromoCodeDto>> CreateAsync(CreatePromoCodeDto dto, CancellationToken cancellationToken = default);
        Task<Result<PromoCodeDto>> UpdateAsync(int id, UpdatePromoCodeDto dto, CancellationToken cancellationToken = default);
        Task<Result<bool>> DeleteAsync(int id, CancellationToken cancellationToken = default);
        Task<Result<bool>> ActivateAsync(int id, CancellationToken cancellationToken = default);
        Task<Result<bool>> DeactivateAsync(int id, CancellationToken cancellationToken = default);
        Task<Result<PromoCodeDto>> GetByIdAsync(int id, CancellationToken cancellationToken = default);
        Task<Result<List<PromoCodeDto>>> FilterAsync(PromoCodeFilterDto filter, CancellationToken cancellationToken = default);
        Task<Result<string>> GenerateCodeAsync(CancellationToken cancellationToken = default);
    }
}
