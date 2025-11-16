using ApplicationLayer.DtoModels.Responses;
using ApplicationLayer.ErrorHnadling;
using ApplicationLayer.Services;
using ApplicationLayer.Services.PaymentProvidersServices;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace DomainLayer.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize(Roles ="SuperAdmin")]
    public class PaymentProviderController : BaseController
    {
        private readonly IPaymentProvidersServices _paymentProviderService;
        private readonly ILogger<PaymentProviderController> _logger;

        public PaymentProviderController(
            ILogger<PaymentProviderController> logger,
            IPaymentProvidersServices paymentProviderService):base(null)
        {
            _logger = logger;
            _paymentProviderService = paymentProviderService;
        }

        [Authorize]
        [HttpPost]
        public async Task<ActionResult<ApiResponse<PaymentProviderDto>>> Create([FromForm] CreatePaymentProviderDto dto)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var result = await _paymentProviderService.CreatePaymentProvider(dto, userId);
            return HandleResult(result, nameof(Create));
        }

        [Authorize]
        [HttpPut("{id}")]
        public async Task<ActionResult<ApiResponse<bool>>> Update(int id, [FromForm] UpdatePaymentProviderDto dto)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var result = await _paymentProviderService.UpdateAsync(id, dto, userId);
            return HandleResult(result, nameof(Update), id);
        }

        [Authorize]
        [HttpDelete("{id}")]
        public async Task<ActionResult<ApiResponse<bool>>> Delete(int id)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var result = await _paymentProviderService.RemovePaymentProviderAsync(id, userId);
            return HandleResult(result, nameof(Delete), id);
        }

        [HttpGet]
        public async Task<ActionResult<ApiResponse<List<PaymentProviderDto>>>> GetAll([FromQuery] bool? isDeleted, [FromQuery] int page = 1, [FromQuery] int pageSize = 10)
        {
            _logger.LogInformation($"GetAll PaymentProviders | isDeleted: {isDeleted}, Page: {page}, PageSize: {pageSize}");
            var result = await _paymentProviderService.GetPaymentProvidersAsync(isDeleted, page, pageSize);
            return HandleResult(result, nameof(GetAll));
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<ApiResponse<PaymentProviderDto>>> GetById(int id, [FromQuery] bool? isDeleted)
        {
            var result = await _paymentProviderService.GetPaymentProviderByIdAsync(id, isDeleted);
            return HandleResult(result, nameof(GetById), id);
        }

        private ActionResult<ApiResponse<T>> HandleResult<T>(Result<T> result, string apiname, int? id = null)
        {
            ApiResponse<T> apiResponse;
            if (result.Success)
            {
                apiResponse = ApiResponse<T>.CreateSuccessResponse(result.Message, result.Data, result.StatusCode, warnings: result.Warnings);
            }
            else
            {
                var errorResponse = (result.Warnings != null && result.Warnings.Count > 0)
                    ? new ErrorResponse("Error", result.Warnings)
                    : new ErrorResponse("Error", result.Message);
                apiResponse = ApiResponse<T>.CreateErrorResponse(result.Message, errorResponse, result.StatusCode, warnings: result.Warnings);
            }

            return result.StatusCode switch
            {
                200 => Ok(apiResponse),
                201 => StatusCode(201, apiResponse),
                400 => BadRequest(apiResponse),
                401 => Unauthorized(apiResponse),
                409 => Conflict(apiResponse),
                _ => StatusCode(result.StatusCode, apiResponse)
            };
        }
    }
}


