using E_Commerce.DtoModels;
using E_Commerce.Models;
using E_Commerce.UOW;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;

namespace E_Commerce.Controllers
{
		[Route("api/[Controller]")]
		[ApiController]
	//[Authorize(Roles ="Admin")]
	
	public class AdminOperationController : ControllerBase
	{
		private readonly IUnitOfWork _unitOfWork;
		private ILogger<AdminOperationController> _Logger;
		public AdminOperationController(ILogger<AdminOperationController> Logger, IUnitOfWork unitOfWork)
		{
			_Logger = Logger;
			_unitOfWork = unitOfWork;
			
		}

		[HttpGet]
		public async Task<ActionResult<ResponseDto>> GetAllOperation()
		{
			_Logger.LogInformation($"Execute:{nameof(GetAllOperation)}");
			var opreations =  _unitOfWork.Repository<AdminOperationsLog>().GetAll();
			if (opreations == null || !opreations.Any())
				return NotFound(new ResponseDto { Message = "No operations found" });

			var list = opreations.Select(x => new
			{
				x.Id,
				x.AdminId,
				x.ItemId,
				x.Description,
				x.CreatedAt
			});
			return Ok(new ResponseDto { Data = list });	
		}
	}
}
