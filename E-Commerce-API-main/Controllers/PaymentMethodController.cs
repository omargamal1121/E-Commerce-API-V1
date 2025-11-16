using ApplicationLayer.DtoModels.Responses;
using ApplicationLayer.Services.PaymentMethodsServices;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace DomainLayer.Controllers
{
	[Route("api/[controller]")]
	[ApiController]
	public class PaymentMethodController : BaseController
	{
		private readonly IPaymentMethodsServices _paymentMethodService;
		private readonly ILogger<PaymentMethodController> _logger;

		public PaymentMethodController(ILogger<PaymentMethodController> logger,IPaymentMethodsServices paymentMethodService)
		{
			_logger = logger;
			_paymentMethodService = paymentMethodService;
		}

	
		[Authorize]
		[HttpDelete("{id}")]
		public async Task<ActionResult<ApiResponse<bool>>> RemovePaymentMethod(int id)
		{
			var userId = GetUserId();
			var result = await _paymentMethodService.RemovePaymentMethod(id, userId);
			return HandleResult(result, nameof(RemovePaymentMethod));
		}

		[Authorize]
		[HttpPost]
		public async Task<ActionResult<ApiResponse<PaymentMethodDto>>> CreatePaymentMethod([FromForm] Createpaymentmethoddto paymentDto)
		{
			var userId = GetUserId();
			var result = await _paymentMethodService.CreatePaymentMethod(paymentDto, userId);
			return HandleResult(result,nameof(CreatePaymentMethod));
		}

		[Authorize]
		[HttpPut("{id}")]
		public async Task<ActionResult<ApiResponse<bool>>> UpdatePaymentMethod(int id, [FromForm] Updatepaymentmethoddto paymentDto)
		{
			var userId = GetUserId();
			var result = await _paymentMethodService.UpdatePaymentMethod(id, paymentDto, userId);
			return HandleResult(result,nameof(UpdatePaymentMethod));
		}
		[HttpPut("Deactivate/{id}")]
		[Authorize(Roles = "Admin,SuperAdmin")]
		public async Task<ActionResult<ApiResponse<bool>>> DeactivatePaymentMethod(int id)
		{
			var userId = GetUserId();

			var result = await _paymentMethodService.DeactivatePaymentMethodAsync(id, userId);

			
			return HandleResult(result,nameof(DeactivatePaymentMethod),id);
		}
		[HttpPut("Activate/{id}")]
		[Authorize(Roles = "Admin,SuperAdmin")]
		public async Task<ActionResult<ApiResponse<bool>>> ActivatePaymentMethod(int id)
		{
			var userId = GetUserId();

			var result = await _paymentMethodService.ActivatePaymentMethodAsync(id, userId);



			return HandleResult(result, nameof(ActivatePaymentMethod), id);
		}
		[HttpGet]
		
		public async Task<ActionResult<ApiResponse<List<PaymentMethodDto>>>> GetAll([FromQuery] bool? isActive, [FromQuery] bool? isDeleted, [FromQuery] int page = 1, [FromQuery] int pageSize = 10)
		{
			_logger.LogInformation($"Admin requested GetAll PaymentMethods | isActive: {isActive}, isDeleted: {isDeleted}, Page: {page}, PageSize: {pageSize}");
			var role = HasManagementRole();
		
			if (!role)
			{
				isActive = true;
				isDeleted = false;
			}

            var result = await _paymentMethodService.GetPaymentMethodsAsync(isActive, isDeleted, page, pageSize);
			return HandleResult(result, nameof(GetAll));
		}
    

	}
}
