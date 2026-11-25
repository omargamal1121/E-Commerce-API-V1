using Application.DtoModels.PromoCodeDtos;
using Application.DtoModels.Responses;
using Application.Services.PromoCodeServices;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace E_Commerce.Controllers
{
    [ApiController]
    [Route("api/admin/promo-codes")]
    [Authorize(Roles = "Admin,SuperAdmin")]
    public class AdminPromoCodeController : BaseController
    {
        private readonly IPromoCodeService _promoCodeService;
        private readonly ILogger<AdminPromoCodeController> _logger;

        public AdminPromoCodeController(IPromoCodeService promoCodeService, ILogger<AdminPromoCodeController> logger)
        {
            _promoCodeService = promoCodeService ?? throw new ArgumentNullException(nameof(promoCodeService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        [HttpPost]
        public async Task<ActionResult<ApiResponse<PromoCodeDto>>> CreateAsync([FromBody] CreatePromoCodeDto dto, CancellationToken ct)
        {
            if (!ModelState.IsValid)
            {
                return CreateErrorResponse<PromoCodeDto>("Validation failed", 400, GetModelErrors());
            }

            _logger.LogInformation("Creating promo code {Code}", dto.Code);
            var result = await _promoCodeService.CreateAsync(dto, ct);
            return HandleResult(result, nameof(CreateAsync));
        }

        [HttpPut("{id:int}")]
        public async Task<ActionResult<ApiResponse<PromoCodeDto>>> UpdateAsync(int id, [FromBody] UpdatePromoCodeDto dto, CancellationToken ct)
        {
            if (!ModelState.IsValid)
            {
                return CreateErrorResponse<PromoCodeDto>("Validation failed", 400, GetModelErrors());
            }

            _logger.LogInformation("Updating promo code {Id}", id);
            var result = await _promoCodeService.UpdateAsync(id, dto, ct);
            return HandleResult(result, nameof(UpdateAsync), id);
        }

        [HttpDelete("{id:int}")]
        public async Task<ActionResult<ApiResponse<bool>>> DeleteAsync(int id, CancellationToken ct)
        {
            _logger.LogInformation("Deleting promo code {Id}", id);
            var result = await _promoCodeService.DeleteAsync(id, ct);
            return HandleResult(result, nameof(DeleteAsync), id);
        }

        [HttpPatch("{id:int}/activate")]
        public async Task<ActionResult<ApiResponse<bool>>> ActivateAsync(int id, CancellationToken ct)
        {
            _logger.LogInformation("Activating promo code {Id}", id);
            var result = await _promoCodeService.ActivateAsync(id, ct);
            return HandleResult(result, nameof(ActivateAsync), id);
        }

        [HttpPatch("{id:int}/deactivate")]
        public async Task<ActionResult<ApiResponse<bool>>> DeactivateAsync(int id, CancellationToken ct)
        {
            _logger.LogInformation("Deactivating promo code {Id}", id);
            var result = await _promoCodeService.DeactivateAsync(id, ct);
            return HandleResult(result, nameof(DeactivateAsync), id);
        }

        [HttpGet]
        public async Task<ActionResult<ApiResponse<List<PromoCodeDto>>>> FilterAsync([FromQuery] PromoCodeFilterDto filter, CancellationToken ct)
        {
            var result = await _promoCodeService.FilterAsync(filter, ct);
            return HandleResult(result, nameof(FilterAsync));
        }

        [HttpGet("{id:int}")]
        public async Task<ActionResult<ApiResponse<PromoCodeDto>>> GetByIdAsync(int id, CancellationToken ct)
        {
            var result = await _promoCodeService.GetByIdAsync(id, ct);
            return HandleResult(result, nameof(GetByIdAsync), id);
        }

        [HttpGet("generate")]
        public async Task<ActionResult<ApiResponse<string>>> GenerateCodeAsync(CancellationToken ct)
        {
            var result = await _promoCodeService.GenerateCodeAsync(ct);
            return HandleResult(result, nameof(GenerateCodeAsync));
        }
    }
}
