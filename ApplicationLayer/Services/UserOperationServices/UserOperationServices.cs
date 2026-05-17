using Application.Interfaces;
using Domain.Enums;
using Domain.Models;
using Infrastructure.Interfaces;
using Microsoft.Extensions.Logging;

namespace Application.Services.UserOperationServices
{
    public class UserOperationServices : IUserOperationServices
    {
        private readonly ILogger<UserOperationServices> _logger;
        private readonly IUnitOfWork _unitOfWork;
        public UserOperationServices(IUnitOfWork unitOfWork, ILogger<UserOperationServices> logger)
        {
            _logger = logger;
            _unitOfWork = unitOfWork;
        }
        public async Task<Result<UserOperationsLog>> AddUserOpreationAsync(string description, Opreations opreation, string userid, int itemid)
        {
            _logger.LogInformation($"Execute {nameof(AddUserOpreationAsync)}");
            var userOperation = new UserOperationsLog
            {
                Description = description,
                UserId = userid,
                ItemId = itemid,
                OperationType = opreation,
            };
            var created = await _unitOfWork.Repository<UserOperationsLog>().CreateAsync(userOperation);
            if (created == null)
            {
                _logger.LogError("Failed to create UserOperationsLog");
                return Result<UserOperationsLog>.Fail("Failed to create UserOperationsLog");
            }
            return Result<UserOperationsLog>.Ok(created);
        }
        public Task<Result<bool>> DeleteUserOpreationAsync(int id)
        {
            throw new NotImplementedException();
        }
        public Task<Result<List<UserOperationsLog>>> GetAllOpreationsAsync()
        {
            throw new NotImplementedException();
        }
        public Task<Result<List<UserOperationsLog>>> GetAllOpreationsByOpreationTypeAsync(Opreations opreation)
        {
            throw new NotImplementedException();
        }
    }
}


