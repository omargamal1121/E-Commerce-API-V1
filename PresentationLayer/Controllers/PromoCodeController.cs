using Application.DtoModels.PromoCodeDtos;
using Application.DtoModels.Responses;
using Application.Services.PromoCodeServices;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Logging;

namespace E_Commerce.Controllers
{
    [ApiController]
    [Route("api/promo-codes")]
    public class PromoCodeController : BaseController
    {
        private readonly IPromoCodeService _promoCodeService;
        private readonly ILogger<PromoCodeController> _logger;

        public PromoCodeController(IPromoCodeService promoCodeService, ILogger<PromoCodeController> logger)
        {
            _promoCodeService = promoCodeService ?? throw new ArgumentNullException(nameof(promoCodeService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        [HttpPost("validate")]
        [AllowAnonymous]
        [EnableRateLimiting("promo-validate")]
        public async Task<ActionResult<ApiResponse<PromoCodeValidationResultDto>>> ValidateAsync([FromBody] ValidatePromoCodeRequestDto request, CancellationToken ct)
        {
            var code = request?.Code ?? string.Empty;
            var result = await _promoCodeService.ValidatePublicAsync(code, ct);
            return HandleResult(result, nameof(ValidateAsync));
        }

        [HttpGet("validate")]
        [AllowAnonymous]
        [EnableRateLimiting("promo-validate")]
        public async Task<ActionResult<ApiResponse<PromoCodeValidationResultDto>>> ValidateGetAsync([FromQuery] string code, CancellationToken ct)
        {
            var result = await _promoCodeService.ValidatePublicAsync(code, ct);
            return HandleResult(result, nameof(ValidateGetAsync));
        }
    }
}
