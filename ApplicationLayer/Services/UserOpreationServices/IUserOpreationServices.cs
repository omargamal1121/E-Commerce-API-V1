using DomainLayer.Enums;
using DomainLayer.Models;

namespace ApplicationLayer.Services.UserOpreationServices
{
    public interface IUserOpreationServices
    {
        public Task<Result<UserOperationsLog>> AddUserOpreationAsync(string description, Opreations opreation, string userid, int itemid);
        public Task<Result<bool>> DeleteUserOpreationAsync(int id);
        public Task<Result<List<UserOperationsLog>>> GetAllOpreationsAsync();
        public Task<Result<List<UserOperationsLog>>> GetAllOpreationsByOpreationTypeAsync(Opreations opreation);
    }
}


