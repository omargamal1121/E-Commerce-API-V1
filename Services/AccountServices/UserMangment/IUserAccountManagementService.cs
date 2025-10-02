namespace E_Commerce.Services.AccountServices.UserMangment
{
	public interface IUserAccountManagementService
    {
        Task<Result<bool>> LockUserAsync(string userId, DateTimeOffset? lockoutEnd = null);
        Task<Result<bool>> UnlockUserAsync(string userId);
        Task<Result<bool>> DeleteUserAsync(string userId);
        Task<Result<bool>> RestoreUserAsync(string userId);
    }

}
