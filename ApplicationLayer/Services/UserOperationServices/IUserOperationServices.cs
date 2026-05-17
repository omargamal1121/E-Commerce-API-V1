using Domain.Enums;
using Domain.Models;

namespace Application.Services.UserOperationServices
{
    public interface IUserOperationServices
    {
        public Task<Result<UserOperationsLog>> AddUserOpreationAsync(string description, Opreations opreation, string userid, int itemid);
        public Task<Result<bool>> DeleteUserOpreationAsync(int id);
        public Task<Result<List<UserOperationsLog>>> GetAllOpreationsAsync();
        public Task<Result<List<UserOperationsLog>>> GetAllOpreationsByOpreationTypeAsync(Opreations opreation);
    }
}


