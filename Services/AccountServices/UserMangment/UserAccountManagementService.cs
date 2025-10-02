using E_Commerce.Models;
using E_Commerce.Services.AdminOperationServices;
using E_Commerce.UOW;
using Microsoft.AspNetCore.Identity;

namespace E_Commerce.Services.AccountServices.UserMangment
{
    public class UserAccountManagementService: IUserAccountManagementService
    {
        private readonly UserManager<Customer> _userManager;
        private readonly IUnitOfWork _unitOfWork;
        private readonly ILogger<UserAccountManagementService> _logger;
        private readonly IAdminOpreationServices _adminOpreationServices;

        public UserAccountManagementService( IUnitOfWork unitOfWork,IAdminOpreationServices adminOpreationServices ,UserManager<Customer> userManager, ILogger<UserAccountManagementService> logger)
        {
            _unitOfWork = unitOfWork;
            _adminOpreationServices = adminOpreationServices;
            _userManager = userManager;
            _logger = logger;
        }

        public async Task<Result<bool>> LockUserAsync(string userId, DateTimeOffset? lockoutEnd = null)
        {

            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
                return Result<bool>.Fail("User not found", 404);

            using var transaction = await _unitOfWork.BeginTransactionAsync();


            await _userManager.SetLockoutEnabledAsync(user, true);
          var result=  await _userManager.SetLockoutEndDateAsync(user, lockoutEnd ?? DateTimeOffset.MaxValue);
            if (!result.Succeeded)
            {
                await transaction.RollbackAsync();
                _logger.LogError("Failed to lock user {UserId}: {Errors}", userId, string.Join(", ", result.Errors.Select(e => e.Description)));
                return Result<bool>.Fail("Failed to lock user", 500);
            }
            // Log the admin operation
            var isadded =await _adminOpreationServices.AddAdminOpreationAsync("Lock User"+ $"Locked user {user.UserName} (ID: {user.Id}) until {lockoutEnd?.ToString() ?? "indefinitely"}",Enums.Opreations.UpdateOpreation,userId,null);
            if (!isadded.Success)
            {
                await transaction.RollbackAsync();
                _logger.LogError("Failed to log admin operation for locking user {UserId}", userId);
                return Result<bool>.Fail("Failed to log admin operation", 500);
            }
            await transaction.CommitAsync();
            await _unitOfWork.CommitAsync();


            _logger.LogInformation("User {UserId} locked until {LockoutEnd}", userId, lockoutEnd);
            return Result<bool>.Ok(true);
        }

        public async Task<Result<bool>> UnlockUserAsync(string userId)
        { 


            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
                return Result<bool>.Fail("User not found", 404);
            using var transaction = await _unitOfWork.BeginTransactionAsync();



         var result=   await _userManager.SetLockoutEndDateAsync(user, null);
            if (!result.Succeeded)
            {
                await transaction.RollbackAsync();
                _logger.LogError("Failed to unlock user {UserId}: {Errors}", userId, string.Join(", ", result.Errors.Select(e => e.Description)));
                return Result<bool>.Fail("Failed to unlock user", 500);
            }

            // Log the admin operation
            var isadded = await _adminOpreationServices.AddAdminOpreationAsync("Unlock User" + $"Unlocked user {user.UserName} (ID: {user.Id})", Enums.Opreations.UpdateOpreation, userId, null);
            if (!isadded.Success)
            {
                await transaction.RollbackAsync();
                _logger.LogError("Failed to log admin operation for unlocking user {UserId}", userId);
                return Result<bool>.Fail("Failed to log admin operation", 500);
            }
            await transaction.CommitAsync();
            await _unitOfWork.CommitAsync();

            _logger.LogInformation("User {UserId} unlocked", userId);
            return Result<bool>.Ok(true);
        }


        public async Task<Result<bool>> DeleteUserAsync(string userId)
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
                return Result<bool>.Fail("User not found", 404);

            using var transaction = await _unitOfWork.BeginTransactionAsync();

            user.DeletedAt = DateTime.UtcNow; 
            var result= await _userManager.UpdateAsync(user);
            if (!result.Succeeded)
            {
                _logger.LogError("Failed to soft delete user {UserId}: {Errors}", userId, string.Join(", ", result.Errors.Select(e => e.Description)));
                await transaction.RollbackAsync();
                return Result<bool>.Fail("Failed to soft delete user", 500);
            }

            // Log the admin operation
            var isadded = await _adminOpreationServices.AddAdminOpreationAsync("Soft Delete User" + $"Soft deleted user {user.UserName} (ID: {user.Id})", Enums.Opreations.DeleteOpreation, userId, null);
            if (!isadded.Success)
            {
                await transaction.RollbackAsync();
                _logger.LogError("Failed to log admin operation for soft deleting user {UserId}", userId);
                return Result<bool>.Fail("Failed to log admin operation", 500);
            }
            await transaction.CommitAsync();
            await _unitOfWork.CommitAsync();

            _logger.LogInformation("User {UserId} soft deleted", userId);
            return Result<bool>.Ok(true);
        }

        public async Task<Result<bool>> RestoreUserAsync(string userId)
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
                return Result<bool>.Fail("User not found", 404);
            using var transaction = await _unitOfWork.BeginTransactionAsync();

            user.DeletedAt = null;
            var result = await _userManager.UpdateAsync(user);
            if (!result.Succeeded)
            {
                _logger.LogError("Failed to restore user {UserId}: {Errors}", userId, string.Join(", ", result.Errors.Select(e => e.Description)));
                await transaction.RollbackAsync();
                return Result<bool>.Fail("Failed to restore user", 500);
            }
            // Log the admin operation
            var isadded = await _adminOpreationServices.AddAdminOpreationAsync("Restore User" + $"Restored user {user.UserName} (ID: {user.Id})", Enums.Opreations.UpdateOpreation, userId, null);
            if (!isadded.Success)
            {
                await transaction.RollbackAsync();
                _logger.LogError("Failed to log admin operation for restoring user {UserId}", userId);
                return Result<bool>.Fail("Failed to log admin operation", 500);
            }
            await transaction.CommitAsync();
            await _unitOfWork.CommitAsync();



            _logger.LogInformation("User {UserId} restored", userId);
            return Result<bool>.Ok(true);
        }
    }

}
