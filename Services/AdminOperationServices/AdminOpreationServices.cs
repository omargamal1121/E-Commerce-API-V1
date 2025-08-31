using E_Commerce.DtoModels.AdminOpreationDtos;
using E_Commerce.Enums;
using E_Commerce.Models;
using E_Commerce.Services.CategoryServcies;
using E_Commerce.UOW;
using Microsoft.EntityFrameworkCore;

namespace E_Commerce.Services.AdminOperationServices
{
	public class AdminOpreationService : IAdminOpreationServices
	{
		private readonly ILogger<AdminOpreationService> _logger;
		private readonly IUnitOfWork _unitOfWork;
		public AdminOpreationService(IUnitOfWork unitOfWork,ILogger<AdminOpreationService> logger)
		{
			_logger = logger;
			_unitOfWork = unitOfWork;
		}
		public async Task<Result<AdminOperationsLog>> AddAdminOpreationAsync(string description, Opreations opreation, string userid, int itemid)
		{
			_logger.LogInformation($"Execute {nameof(AddAdminOpreationAsync)}");
			var adminopreation = new AdminOperationsLog
			{
				Description = description,
				AdminId = userid,
				ItemId = new List<int> { itemid},
				OperationType = opreation,
			};
			var created = await _unitOfWork.Repository<AdminOperationsLog>().CreateAsync(adminopreation);
			if (created == null)
			{
				_logger.LogError("Failed to create AdminOperationsLog");
				return Result<AdminOperationsLog>.Fail("Failed to create AdminOperationsLog");
			}
			return Result<AdminOperationsLog>.Ok(created);
		}
		public async Task<Result<AdminOperationsLog>> AddAdminOpreationAsync(string description, Opreations opreation, string userid, List<int>itemids)
		{
			_logger.LogInformation($"Execute {nameof(AddAdminOpreationAsync)}");
			var adminopreation = new AdminOperationsLog
			{
				Description = description,
				AdminId = userid,
				ItemId = itemids,
				OperationType = opreation,
			};
			var created = await _unitOfWork.Repository<AdminOperationsLog>().CreateAsync(adminopreation);
			if (created == null)
			{
				_logger.LogError("Failed to create AdminOperationsLog");
				return Result<AdminOperationsLog>.Fail("Failed to create AdminOperationsLog");
			}
			return Result<AdminOperationsLog>.Ok(created);
		}
		public async Task<Result<List<OpreationDto>>> GetAllOpreationsAsync(string? userid=null,string?name=null,Opreations? opreation=null)
		{
			_logger.LogInformation($"Execute {nameof(GetAllOpreationsAsync)}");
			var adminopreations =  _unitOfWork.Repository<AdminOperationsLog>().GetAll();
			if(!string.IsNullOrEmpty(userid))
				adminopreations = adminopreations.Where(a => a.AdminId == userid);
			if(!string.IsNullOrEmpty(name))
				adminopreations = adminopreations.Where(a => a.Admin.Name.Contains(name));
			if(opreation != null)
				adminopreations = adminopreations.Where(a => a.OperationType == opreation);
			if (adminopreations == null || !adminopreations.Any())
				return Result<List<OpreationDto>>.Fail("No admin operations found",404);
			var adminopreationDtos = await adminopreations.Select(o => new OpreationDto
			{
				Description = o.Description,
				Id = o.Id.ToString(),
				ItemId = o.ItemId,
				Name = o.Admin.Name,
				OperationType = o.OperationType.ToString(),
				Timestamp = o.Timestamp,
			}).ToListAsync();
			return Result<List<OpreationDto>>.Ok(adminopreationDtos);
		}
			

	
	}
}
