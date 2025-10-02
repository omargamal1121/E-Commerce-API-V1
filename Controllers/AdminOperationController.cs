using E_Commerce.DtoModels;
using E_Commerce.Models;
using E_Commerce.Services.AdminOperationServices;
using E_Commerce.UOW;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;

namespace E_Commerce.Controllers
{
		[Route("api/[Controller]")]
		[ApiController]
		[Authorize(Roles = "Admin,SuperAdmin")]
	
	public class AdminOperationController : ControllerBase
	{
	
		private ILogger<AdminOperationController> _Logger;
		private readonly IAdminOpreationServices _adminOpreationServices;
		public AdminOperationController(IAdminOpreationServices adminOpreationServices , ILogger<AdminOperationController> Logger)
		{
			_Logger = Logger;
			_adminOpreationServices = adminOpreationServices;

		}

		[HttpGet]
		public async Task<ActionResult<ResponseDto>> GetAllOperation()
		{
			_Logger.LogInformation($"Execute {nameof(GetAllOperation)}");
			var result = await _adminOpreationServices.GetAllOpreationsAsync();
			if (!result.Success)
			{
				_Logger.LogError(result.Message);
				return BadRequest(new ResponseDto(result.Message));
			}
			return Ok(new ResponseDto("Admin operations retrieved successfully", result.Data));

		}
	}
}
