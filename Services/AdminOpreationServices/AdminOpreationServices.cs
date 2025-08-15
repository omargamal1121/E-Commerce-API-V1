using E_Commerce.Enums;
using E_Commerce.Models;
using E_Commerce.Services.CategoryServcies;
using E_Commerce.UOW;

namespace E_Commerce.Services.AdminOpreationServices
{
	public class AdminOpreationServices : IAdminOpreationServices
	{
		private readonly ILogger<AdminOpreationServices> _logger;
		private readonly IUnitOfWork _unitOfWork;
		public AdminOpreationServices(IUnitOfWork unitOfWork,ILogger<AdminOpreationServices> logger)
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

		public Task<Result<bool>> DeleteAdminOpreationAsync(int id)
		{
			throw new NotImplementedException();
		}

		public Task<Result<List<AdminOperationsLog>>> GetAllOpreationsAsync()
		{
			throw new NotImplementedException();
		}

		public Task<Result<List<AdminOperationsLog>>> GetAllOpreationsByOpreationTypeAsync(Opreations opreation)
		{
			throw new NotImplementedException();
		}
	}
}
