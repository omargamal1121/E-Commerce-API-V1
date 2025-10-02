namespace E_Commerce.Services.AccountServices.UserMangment
{
	public interface IUserRoleMangementService
    {
        Task<Result<bool>> AddRoleToUserAsync(string id, string role);
        Task<Result<bool>> RemoveRoleFromUserAsync(string id, string role);
        Task<Result<List<string>>> GetAllRolesAsync();

    }
}
