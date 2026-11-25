using System.Security.Cryptography;
using System.Text.RegularExpressions;
using Application.DtoModels.PromoCodeDtos;
using Domain.Enums;
using Domain.Models;
using Hangfire;
using Infrastructure.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Application.Services.PromoCodeServices
{
    public class PromoCodeService : IPromoCodeService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly ILogger<PromoCodeService> _logger;
        private readonly IBackgroundJobClient? _backgroundJobClient;
        private static readonly Regex CodeRegex = new(@"^[A-Z0-9_-]{3,20}$", RegexOptions.Compiled);
        private const string AllowedCharset = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";

        public PromoCodeService(
            IUnitOfWork unitOfWork, 
            ILogger<PromoCodeService> logger, 
            IBackgroundJobClient? backgroundJobClient = null)
        {
            _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _backgroundJobClient = backgroundJobClient;
        }

        public async Task<PromoCodeValidationResult> ValidatePromoCodeAsync(string? code, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(code))
            {
                return PromoCodeValidationResult.Failure("NotFound");
            }

            var normalizedCode = code.Trim().ToUpperInvariant();
            if (!CodeRegex.IsMatch(normalizedCode))
            {
                return PromoCodeValidationResult.Failure("NotFound");
            }

            var promoCode = await _unitOfWork.PromoCode.GetByCodeAsync(normalizedCode, includeDeleted: false, asNoTracking: true);
            if (promoCode == null)
            {
                return PromoCodeValidationResult.Failure("NotFound");
            }

            if (!promoCode.IsActive)
            {
                return PromoCodeValidationResult.Failure("Inactive");
            }

            var now = DateTime.UtcNow;
            if (promoCode.StartsAt > now)
            {
                return PromoCodeValidationResult.Failure("NotStarted");
            }

            if (promoCode.EndsAt < now)
            {
                return PromoCodeValidationResult.Failure("Expired");
            }

            return PromoCodeValidationResult.Success(
                promoCode.Id,
                promoCode.Code,
                promoCode.DiscountPercent,
                promoCode.EndsAt
            );
        }

        public async Task<Result<PromoCodeValidationResultDto>> ValidatePublicAsync(string? code, CancellationToken cancellationToken = default)
        {
            var result = await ValidatePromoCodeAsync(code, cancellationToken);

            var dto = new PromoCodeValidationResultDto
            {
                IsValid = result.IsValid,
                Code = result.Code,
                DiscountPercent = result.DiscountPercent,
                EndsAt = result.EndsAt,
                Reason = result.Reason
            };

            return Result<PromoCodeValidationResultDto>.Ok(dto, "Promo code validation completed", 200);
        }

        public async Task<Result<PromoCodeDto>> CreateAsync(CreatePromoCodeDto dto, CancellationToken cancellationToken = default)
        {
            if (dto == null)
            {
                return Result<PromoCodeDto>.Fail("Invalid promo code data", 400);
            }

            var normalizedCode = dto.Code.Trim().ToUpperInvariant();
            if (!CodeRegex.IsMatch(normalizedCode))
            {
                return Result<PromoCodeDto>.Fail("Promo code can only contain uppercase letters, numbers, hyphens, and underscores (3-20 characters).", 400);
            }

            var startsAtUtc = dto.StartsAt.ToUniversalTime();
            var endsAtUtc = dto.EndsAt.ToUniversalTime();
            var now = DateTime.UtcNow;

            if (endsAtUtc <= startsAtUtc)
            {
                return Result<PromoCodeDto>.Fail("End date must be after start date.", 400);
            }

            if (endsAtUtc <= now)
            {
                return Result<PromoCodeDto>.Fail("End date must be in the future.", 400);
            }

            if (dto.DiscountPercent <= 0 || dto.DiscountPercent > 100)
            {
                return Result<PromoCodeDto>.Fail("Discount percent must be greater than 0 and less than or equal to 100.", 400);
            }

            var exists = await _unitOfWork.PromoCode.CodeExistsAsync(normalizedCode);
            if (exists)
            {
                return Result<PromoCodeDto>.Fail($"Promo code '{normalizedCode}' already exists.", 409);
            }

            var promoCode = new PromoCode
            {
                Code = normalizedCode,
                DiscountPercent = dto.DiscountPercent,
                StartsAt = startsAtUtc,
                EndsAt = endsAtUtc,
                IsActive = dto.IsActive,
                CreatedAt = now
            };

            await _unitOfWork.PromoCode.CreateAsync(promoCode);
            await _unitOfWork.CommitAsync();

            _logger.LogInformation("Promo code {Code} created by admin", promoCode.Code);

            // Schedule Hangfire background jobs for automatic activation and deactivation
            ScheduleHangfireJobs(promoCode, startsAtUtc, endsAtUtc, now);

            return Result<PromoCodeDto>.Ok(MapToDto(promoCode, 0), "Promo code created successfully", 201);
        }

        public async Task<Result<PromoCodeDto>> UpdateAsync(int id, UpdatePromoCodeDto dto, CancellationToken cancellationToken = default)
        {
            if (dto == null)
            {
                return Result<PromoCodeDto>.Fail("Invalid promo code data", 400);
            }

            var promoCode = await _unitOfWork.PromoCode.GetByIdAsync(id);
            if (promoCode == null || promoCode.DeletedAt != null)
            {
                return Result<PromoCodeDto>.Fail("Promo code not found", 404);
            }

            var normalizedCode = dto.Code.Trim().ToUpperInvariant();
            if (!CodeRegex.IsMatch(normalizedCode))
            {
                return Result<PromoCodeDto>.Fail("Promo code can only contain uppercase letters, numbers, hyphens, and underscores (3-20 characters).", 400);
            }

            var ordersCount = await _unitOfWork.PromoCode.GetOrderCountForPromoCodeAsync(id);

            // If Code is changed, check if it was ever used
            if (!string.Equals(promoCode.Code, normalizedCode, StringComparison.OrdinalIgnoreCase))
            {
                if (ordersCount > 0)
                {
                    return Result<PromoCodeDto>.Fail("Cannot change promo code text because it has already been used in existing orders.", 400);
                }

                var exists = await _unitOfWork.PromoCode.CodeExistsAsync(normalizedCode, excludeId: id);
                if (exists)
                {
                    return Result<PromoCodeDto>.Fail($"Promo code '{normalizedCode}' is already taken.", 409);
                }

                promoCode.Code = normalizedCode;
            }

            var startsAtUtc = dto.StartsAt.ToUniversalTime();
            var endsAtUtc = dto.EndsAt.ToUniversalTime();
            var now = DateTime.UtcNow;

            if (endsAtUtc <= startsAtUtc)
            {
                return Result<PromoCodeDto>.Fail("End date must be after start date.", 400);
            }

            if (endsAtUtc <= now)
            {
                return Result<PromoCodeDto>.Fail("End date must be in the future.", 400);
            }

            if (dto.DiscountPercent <= 0 || dto.DiscountPercent > 100)
            {
                return Result<PromoCodeDto>.Fail("Discount percent must be greater than 0 and less than or equal to 100.", 400);
            }

            promoCode.DiscountPercent = dto.DiscountPercent;
            promoCode.StartsAt = startsAtUtc;
            promoCode.EndsAt = endsAtUtc;
            promoCode.IsActive = dto.IsActive;
            promoCode.ModifiedAt = now;

            _unitOfWork.PromoCode.Update(promoCode);
            await _unitOfWork.CommitAsync();

            _logger.LogInformation("Promo code {Code} (ID: {Id}) updated by admin", promoCode.Code, id);

            // Reschedule/schedule Hangfire jobs if dates updated
            ScheduleHangfireJobs(promoCode, startsAtUtc, endsAtUtc, now);

            return Result<PromoCodeDto>.Ok(MapToDto(promoCode, ordersCount), "Promo code updated successfully", 200);
        }

        public async Task<Result<bool>> DeleteAsync(int id, CancellationToken cancellationToken = default)
        {
            var promoCode = await _unitOfWork.PromoCode.GetByIdAsync(id);
            if (promoCode == null || promoCode.DeletedAt != null)
            {
                return Result<bool>.Fail("Promo code not found", 404);
            }

            // Per requirement: if the code is used by any order, soft delete only; never hard delete.
            promoCode.DeletedAt = DateTime.UtcNow;
            promoCode.IsActive = false;
            promoCode.ModifiedAt = DateTime.UtcNow;

            _unitOfWork.PromoCode.Update(promoCode);
            await _unitOfWork.CommitAsync();

            _logger.LogInformation("Promo code {Code} (ID: {Id}) soft deleted by admin", promoCode.Code, id);
            return Result<bool>.Ok(true, "Promo code deleted successfully", 200);
        }

        public async Task<Result<bool>> ActivateAsync(int id, CancellationToken cancellationToken = default)
        {
            var promoCode = await _unitOfWork.PromoCode.GetByIdAsync(id);
            if (promoCode == null || promoCode.DeletedAt != null)
            {
                return Result<bool>.Fail("Promo code not found", 404);
            }

            if (promoCode.EndsAt <= DateTime.UtcNow)
            {
                return Result<bool>.Fail("Cannot activate an expired promo code. Please extend the end date first.", 400);
            }

            promoCode.IsActive = true;
            promoCode.ModifiedAt = DateTime.UtcNow;

            _unitOfWork.PromoCode.Update(promoCode);
            await _unitOfWork.CommitAsync();

            _logger.LogInformation("Promo code {Code} (ID: {Id}) activated by admin", promoCode.Code, id);
            return Result<bool>.Ok(true, "Promo code activated successfully", 200);
        }

        public async Task<Result<bool>> DeactivateAsync(int id, CancellationToken cancellationToken = default)
        {
            var promoCode = await _unitOfWork.PromoCode.GetByIdAsync(id);
            if (promoCode == null || promoCode.DeletedAt != null)
            {
                return Result<bool>.Fail("Promo code not found", 404);
            }

            promoCode.IsActive = false;
            promoCode.ModifiedAt = DateTime.UtcNow;

            _unitOfWork.PromoCode.Update(promoCode);
            await _unitOfWork.CommitAsync();

            _logger.LogInformation("Promo code {Code} (ID: {Id}) deactivated by admin", promoCode.Code, id);
            return Result<bool>.Ok(true, "Promo code deactivated successfully", 200);
        }

        public async Task<Result<PromoCodeDto>> GetByIdAsync(int id, CancellationToken cancellationToken = default)
        {
            var promoCode = await _unitOfWork.PromoCode.GetByIdAsync(id);
            if (promoCode == null || promoCode.DeletedAt != null)
            {
                return Result<PromoCodeDto>.Fail("Promo code not found", 404);
            }

            var ordersCount = await _unitOfWork.PromoCode.GetOrderCountForPromoCodeAsync(id);
            return Result<PromoCodeDto>.Ok(MapToDto(promoCode, ordersCount), "Promo code retrieved successfully", 200);
        }

        public async Task<Result<List<PromoCodeDto>>> FilterAsync(PromoCodeFilterDto filter, CancellationToken cancellationToken = default)
        {
            filter ??= new PromoCodeFilterDto();
            var page = Math.Max(1, filter.Page);
            var pageSize = Math.Clamp(filter.PageSize, 1, 100);

            var query = _unitOfWork.PromoCode.GetQueryable(includeDeleted: false, asNoTracking: true);

            if (!string.IsNullOrWhiteSpace(filter.Search))
            {
                var term = filter.Search.Trim().ToUpperInvariant();
                query = query.Where(p => p.Code.Contains(term));
            }

            var now = DateTime.UtcNow;

            if (filter.Status.HasValue)
            {
                switch (filter.Status.Value)
                {
                    case PromoCodeStatus.Active:
                        query = query.Where(p => p.IsActive && p.StartsAt <= now && p.EndsAt > now);
                        break;
                    case PromoCodeStatus.Inactive:
                        query = query.Where(p => !p.IsActive && p.EndsAt > now);
                        break;
                    case PromoCodeStatus.Expired:
                        query = query.Where(p => p.EndsAt <= now);
                        break;
                    case PromoCodeStatus.Upcoming:
                        query = query.Where(p => p.StartsAt > now && p.EndsAt > now);
                        break;
                }
            }

            // Ordering deterministically by StartsAt desc, then Id desc for stable pagination
            var pagedPromoCodes = await query
                .OrderByDescending(p => p.StartsAt)
                .ThenByDescending(p => p.Id)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(p => new
                {
                    p.Id,
                    p.Code,
                    p.DiscountPercent,
                    p.StartsAt,
                    p.EndsAt,
                    p.IsActive,
                    p.CreatedAt,
                    p.ModifiedAt,
                    OrdersCount = p.Orders.Count()
                })
                .ToListAsync(cancellationToken);

            var dtos = pagedPromoCodes.Select(p => new PromoCodeDto
            {
                Id = p.Id,
                Code = p.Code,
                DiscountPercent = p.DiscountPercent,
                StartsAt = p.StartsAt,
                EndsAt = p.EndsAt,
                IsActive = p.IsActive,
                Status = ComputeStatus(p.IsActive, p.StartsAt, p.EndsAt, now),
                OrdersCount = p.OrdersCount,
                CreatedAt = p.CreatedAt,
                ModifiedAt = p.ModifiedAt
            }).ToList();

            return Result<List<PromoCodeDto>>.Ok(dtos, "Promo codes retrieved successfully", 200);
        }

        public async Task<Result<string>> GenerateCodeAsync(CancellationToken cancellationToken = default)
        {
            const int length = 8;
            string code;
            bool exists;

            do
            {
                var chars = new char[length];
                for (int i = 0; i < length; i++)
                {
                    var idx = RandomNumberGenerator.GetInt32(AllowedCharset.Length);
                    chars[i] = AllowedCharset[idx];
                }
                code = new string(chars);
                exists = await _unitOfWork.PromoCode.CodeExistsAsync(code);
            } while (exists);

            return Result<string>.Ok(code, "Generated unique promo code", 200);
        }

        private void ScheduleHangfireJobs(PromoCode promoCode, DateTime startsAtUtc, DateTime endsAtUtc, DateTime now)
        {
            if (_backgroundJobClient == null)
            {
                return;
            }

            try
            {
                // 1. Job responsible for activating the promo code at StartsAt
                if (startsAtUtc > now)
                {
                    var activateDelay = startsAtUtc - now;
                    _backgroundJobClient.Schedule<IPromoCodeJobService>(
                        job => job.ActivatePromoCodeJobAsync(promoCode.Id),
                        activateDelay);

                    _logger.LogInformation("Scheduled Hangfire activation job for promo code {Code} (ID: {Id}) in {Delay}", promoCode.Code, promoCode.Id, activateDelay);
                }

                // 2. Job responsible for deactivating the promo code at EndsAt
                if (endsAtUtc > now)
                {
                    var deactivateDelay = endsAtUtc - now;
                    _backgroundJobClient.Schedule<IPromoCodeJobService>(
                        job => job.DeactivatePromoCodeJobAsync(promoCode.Id),
                        deactivateDelay);

                    _logger.LogInformation("Scheduled Hangfire deactivation job for promo code {Code} (ID: {Id}) in {Delay}", promoCode.Code, promoCode.Id, deactivateDelay);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to schedule Hangfire jobs for promo code {Code} (ID: {Id})", promoCode.Code, promoCode.Id);
            }
        }

        private static PromoCodeDto MapToDto(PromoCode entity, int ordersCount)
        {
            var now = DateTime.UtcNow;
            return new PromoCodeDto
            {
                Id = entity.Id,
                Code = entity.Code,
                DiscountPercent = entity.DiscountPercent,
                StartsAt = entity.StartsAt,
                EndsAt = entity.EndsAt,
                IsActive = entity.IsActive,
                Status = ComputeStatus(entity.IsActive, entity.StartsAt, entity.EndsAt, now),
                OrdersCount = ordersCount,
                CreatedAt = entity.CreatedAt,
                ModifiedAt = entity.ModifiedAt
            };
        }

        private static PromoCodeStatus ComputeStatus(bool isActive, DateTime startsAt, DateTime endsAt, DateTime now)
        {
            if (endsAt <= now)
                return PromoCodeStatus.Expired;
            if (startsAt > now)
                return PromoCodeStatus.Upcoming;
            if (!isActive)
                return PromoCodeStatus.Inactive;
            return PromoCodeStatus.Active;
        }
    }
}
