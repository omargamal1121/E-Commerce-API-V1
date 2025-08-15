using E_Commerce.DtoModels.EnumDtos;
using E_Commerce.DtoModels.Responses;
using E_Commerce.Enums;
using E_Commerce.Services.EnumServices;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace E_Commerce.Controllers
{
	[Route("api/[controller]")]
	[ApiController]
	public class EnumsController : ControllerBase
	{
		private readonly ILogger<EnumsController> _logger;
		public EnumsController(ILogger<EnumsController> logger)
		{
			_logger = logger;
		}
		[HttpGet("PaymentStatus")]
		public ActionResult<ApiResponse< List<EnumDto>>> GetPaymentStatuse()
		{
			return ApiResponse<List<EnumDto>>.CreateSuccessResponse("Retrevie Success", EnumServices.ToSelectList<PaymentStatus>());
		}
		[HttpGet("PaymentMethods")]
		public ActionResult<ApiResponse< List<EnumDto>>> GetPaymentMethod()
		{
			return ApiResponse<List<EnumDto>>.CreateSuccessResponse("Retrevie Success", EnumServices.ToSelectList<PaymentMethodEnums>());
		}
		[HttpGet("PaymentProviders")]
		public ActionResult<ApiResponse< List<EnumDto>>> GetPaymentProviders()
		{
			return ApiResponse<List<EnumDto>>.CreateSuccessResponse("Retrevie Success", EnumServices.ToSelectList<PaymentProviderEnums>());
		}
		[HttpGet("Genders")]
		public ActionResult<ApiResponse< List<EnumDto>>> GetGenders()
		{
			return ApiResponse<List<EnumDto>>.CreateSuccessResponse("Retrevie Success", EnumServices.ToSelectList<Gender>());
		}
		[HttpGet("Sizes")]
		public ActionResult<ApiResponse< List<EnumDto>>> GetSizes()
		{
			return ApiResponse<List<EnumDto>>.CreateSuccessResponse("Retrevie Success", EnumServices.ToSelectList<VariantSize>());
		}
	}
}
