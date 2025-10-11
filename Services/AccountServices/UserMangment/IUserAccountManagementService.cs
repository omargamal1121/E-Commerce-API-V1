namespace E_Commerce.Services.AccountServices.UserMangment
{
	public interface IUserAccountManagementService
    {
        Task<Result<bool>> LockUserAsync(string userId,string adminId, DateTimeOffset? lockoutEnd = null);
        Task<Result<bool>> UnlockUserAsync(string userId,string adminId);
        Task<Result<bool>> DeleteUserAsync(string userId, string adminId);
        Task<Result<bool>> RestoreUserAsync(string userId, string adminId);
    }

}
