using DomainLayer.Enums;
using DomainLayer.Models;
using ApplicationLayer.Services;
using ApplicationLayer.Services.AdminOperationServices;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using ApplicationLayer.Interfaces;

namespace ApplicationLayer.Services.AccountServices.UserMangment
{
    public class UserAccountManagementService : IUserAccountManagementService
    {
        private readonly UserManager<Customer> _userManager;
        private readonly IUnitOfWork _unitOfWork;
        private readonly ILogger<UserAccountManagementService> _logger;
        private readonly IAdminOpreationServices _adminOpreationServices;

        public UserAccountManagementService(
            IUnitOfWork unitOfWork,
            IAdminOpreationServices adminOpreationServices,
            UserManager<Customer> userManager,
            ILogger<UserAccountManagementService> logger)
        {
            _unitOfWork = unitOfWork;
            _adminOpreationServices = adminOpreationServices;
            _userManager = userManager;
            _logger = logger;
        }

        #region Helpers
        private async Task< Result<bool>>ValidateIds(string userId, string adminId)
        {
            if (string.IsNullOrWhiteSpace(userId))
            {
                _logger.LogWarning("Validation failed: empty UserId received by Admin {AdminId}", adminId);
                return Result<bool>.Fail("Invalid UserId", 400);
            }

            if (string.IsNullOrWhiteSpace(adminId))
            {
                _logger.LogWarning("Validation failed: empty AdminId while managing User {UserId}", userId);
                return Result<bool>.Fail("Invalid AdminId", 400);
            }

            if (userId == adminId)
            {
                _logger.LogWarning("Admin {AdminId} attempted to modify their own account", adminId);
                return Result<bool>.Fail("You cannot modify your own account", 403);
            }
            var admin = await _userManager.FindByIdAsync(adminId);
            if (admin == null || !await _userManager.IsInRoleAsync(admin, "SuperAdmin"))
                return Result<bool>.Fail("Unauthorized operation", 403);


            return Result<bool>.Ok(true);
        }

        private async Task<Customer?> FindUserAsync(string userId)
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
                _logger.LogWarning("User not found with ID {UserId}", userId);
            else
                _logger.LogInformation("Fetched user {UserName} (ID: {UserId})", user.UserName, user.Id);

            return user;
        }

        private async Task<Result<bool>> ExecuteWithTransactionAsync(
            Func<Task<IdentityResult>> userOperation,
            string adminOperationDesc,
			Opreations opType,
            string userId,
            string adminId)
        {
            _logger.LogInformation("Admin {AdminId} started {Operation} for User {UserId}", adminId, opType, userId);

            using var transaction = await _unitOfWork.BeginTransactionAsync();

            try
            {
                var result = await userOperation();
                if (!result.Succeeded)
                {
                    await transaction.RollbackAsync();
                    var errors = string.Join(", ", result.Errors.Select(e => e.Description));

                    _logger.LogError("Admin {AdminId} failed {Operation} for User {UserId}: {Errors}",
                        adminId, opType, userId, errors);

                    return Result<bool>.Fail(errors, 500);
                }

                var logResult = await _adminOpreationServices.AddAdminOpreationAsync(
                    adminOperationDesc, opType, userId, null);

                if (!logResult.Success)
                {
                    await transaction.RollbackAsync();
                    _logger.LogError("Failed to log admin operation {Operation} for User {UserId}", opType, userId);
                    return Result<bool>.Fail("Failed to log admin operation", 500);
                }


                await _unitOfWork.CommitAsync();
                await transaction.CommitAsync();

                _logger.LogInformation("Admin {AdminId} successfully completed {Operation} for User {UserId}",
                    adminId, opType, userId);

                return Result<bool>.Ok(true);
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Unexpected error during {Operation} for User {UserId} by Admin {AdminId}",
                    opType, userId, adminId);

                return Result<bool>.Fail("Unexpected server error", 500);
            }
        }
        #endregion

        #region Public Methods
        public async Task<Result<bool>> LockUserAsync(string userId, string adminId, DateTimeOffset? lockoutEnd = null)
        {
            _logger.LogInformation("Admin {AdminId} requested to lock User {UserId}", adminId, userId);

            var validation = await ValidateIds(userId, adminId);
            if (!validation.Success) return  validation;

            var user = await FindUserAsync(userId);
            if (user == null) return Result<bool>.Fail("User not found", 404);

            var lockDate = lockoutEnd ?? DateTimeOffset.UtcNow.AddYears(100);

            return await ExecuteWithTransactionAsync(
                async () =>
                {
                    await _userManager.SetLockoutEnabledAsync(user, true);
                    return await _userManager.SetLockoutEndDateAsync(user, lockDate);
                },
                $"Locked user {user.UserName} (ID: {user.Id}) until {lockDate}",
				Opreations.UpdateOpreation,
                userId,
                adminId
            );
        }

        public async Task<Result<bool>> UnlockUserAsync(string userId, string adminId)
        {
            _logger.LogInformation("Admin {AdminId} requested to unlock User {UserId}", adminId, userId);

            var validation = await ValidateIds(userId, adminId);
            if (!validation.Success) return validation;

            var user = await FindUserAsync(userId);
            if (user == null) return Result<bool>.Fail("User not found", 404);

            return await ExecuteWithTransactionAsync(
                () => _userManager.SetLockoutEndDateAsync(user, null),
                $"Unlocked user {user.UserName} (ID: {user.Id})",
				Opreations.UpdateOpreation,
                userId,
                adminId
            );
        }

        public async Task<Result<bool>> DeleteUserAsync(string userId, string adminId)
        {
            _logger.LogInformation("Admin {AdminId} requested to delete User {UserId}", adminId, userId);

            var validation = await ValidateIds(userId, adminId);
            if (!validation.Success) return validation;

            var user = await FindUserAsync(userId);
            if (user == null) return Result<bool>.Fail("User not found", 404);

            user.DeletedAt = DateTime.UtcNow;

            return await ExecuteWithTransactionAsync(
                () => _userManager.UpdateAsync(user),
                $"Soft deleted user {user.UserName} (ID: {user.Id}) by Admin({adminId})",
				Opreations.DeleteOpreation,
                userId,
                adminId
            );
        }

        public async Task<Result<bool>> RestoreUserAsync(string userId, string adminId)
        {
            _logger.LogInformation("Admin {AdminId} requested to restore User {UserId}", adminId, userId);

            var validation = await ValidateIds(userId, adminId);
            if (!validation.Success) return validation;

            var user = await FindUserAsync(userId);
            if (user == null) return Result<bool>.Fail("User not found", 404);

            user.DeletedAt = null;

            return await ExecuteWithTransactionAsync(
                () => _userManager.UpdateAsync(user),
                $"Restored user {user.UserName} (ID: {user.Id})",
				Opreations.UpdateOpreation,
                userId,
                adminId
            );
        }
        #endregion
    }
}


