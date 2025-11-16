using ApplicationLayer.Services;

namespace ApplicationLayer.Services.AccountServices.UserMangment
{
	public interface IUserRoleMangementService
    {
        Task<Result<bool>> AddRoleToUserAsync(string id, string role,string userid);
        Task<Result<bool>> RemoveRoleFromUserAsync(string id, string role,string userid);
        Task<Result<List<string>>> GetAllRolesAsync();

    }
}


