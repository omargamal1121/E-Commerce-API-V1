using ApplicationLayer.Services.AccountServices.UserMangment;
using DomainLayer.Enums;
using DomainLayer.Models;
using ApplicationLayer.Services.AdminOperationServices;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ApplicationLayer.Interfaces;

namespace ApplicationLayer.Services.AccountServices.UserMangment
{
    public class UserRoleMangementService : IUserRoleMangementService
    {
        private readonly UserManager<Customer> _userManager;
        private readonly IAdminOpreationServices _adminOpreationServices;
        private readonly IUnitOfWork _unitOfWork;
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly ILogger<UserRoleMangementService> _logger;

        public UserRoleMangementService(
            IUnitOfWork unitOfWork,
            IAdminOpreationServices adminOpreationServices,
            UserManager<Customer> userManager,
            RoleManager<IdentityRole> roleManager,
            ILogger<UserRoleMangementService> logger)
        {
            _unitOfWork = unitOfWork;
            _adminOpreationServices = adminOpreationServices;
            _userManager = userManager;
            _roleManager = roleManager;
            _logger = logger;
        }

        #region Helpers

        private async Task<Result<bool>> ValidateIdsAndRole(string userId, string adminId, string role)
        {
            _logger.LogInformation("Starting validation for UserId: {UserId}, AdminId: {AdminId}, Role: {Role}", userId, adminId, role);

            if (string.IsNullOrWhiteSpace(userId))
            {
                _logger.LogWarning("Validation failed: UserId is empty or null.");
                return Result<bool>.Fail("Invalid UserId", 400);
            }

            if (string.IsNullOrWhiteSpace(adminId))
            {
                _logger.LogWarning("Validation failed: AdminId is empty or null.");
                return Result<bool>.Fail("Invalid AdminId", 400);
            }

            if (userId == adminId)
            {
                _logger.LogWarning("Validation failed: Admin {AdminId} tried to modify their own roles.", adminId);
                return Result<bool>.Fail("You cannot modify your own roles", 403);
            }

            if (string.IsNullOrWhiteSpace(role))
            {
                _logger.LogWarning("Validation failed: Role is empty or null.");
                return Result<bool>.Fail("Invalid role", 400);
            }

            var adminUser = await _userManager.FindByIdAsync(adminId);
            if (adminUser is null || adminUser.DeletedAt != null)
            {
                _logger.LogWarning("Validation failed: Admin user {AdminId} not found or marked as deleted.", adminId);
                return Result<bool>.Fail("Admin user not found", 404);
            }

            if (!await _userManager.IsInRoleAsync(adminUser, "SuperAdmin"))
            {
                _logger.LogWarning("Validation failed: Admin {AdminId} is not in role 'SuperAdmin'.", adminId);
                return Result<bool>.Fail("Admin does not have sufficient permissions", 403);
            }

            if (!await _roleManager.RoleExistsAsync(role))
            {
                _logger.LogWarning("Validation failed: Role '{Role}' does not exist.", role);
                return Result<bool>.Fail("Role does not exist", 400);
            }

            _logger.LogInformation("Validation succeeded for UserId: {UserId}, Role: {Role}", userId, role);
            return Result<bool>.Ok(true);
        }

        private async Task<Result<bool>> ExecuteRoleChangeAsync(
            string operationName,
            Func<Task<IdentityResult>> roleOperation,
            string adminOperationDesc,
            string userId,
            string role,
            string adminId,
			Opreations opType)
        {
            _logger.LogInformation("Starting role {Operation} operation for UserId: {UserId}, Role: {Role}, AdminId: {AdminId}",
                operationName, userId, role, adminId);

            using var transaction = await _unitOfWork.BeginTransactionAsync();

            try
            {
                var result = await roleOperation();

                if (!result.Succeeded)
                {
                    await transaction.RollbackAsync();
                    var errors = string.Join(", ", result.Errors.Select(e => e.Description));
                    _logger.LogError("Failed to {Operation} role '{Role}' for user {UserId}. Errors: {Errors}",
                        operationName, role, userId, errors);

                    return Result<bool>.Fail(errors, 400);
                }

                _logger.LogInformation("Successfully executed role {Operation} for user {UserId}. Logging admin operation...", operationName, userId);

                var logResult = await _adminOpreationServices.AddAdminOpreationAsync(
                    adminOperationDesc, opType, userId, null);

                if (!logResult.Success)
                {
                    await transaction.RollbackAsync();
                    _logger.LogError("Failed to log admin operation {Operation} for user {UserId}", operationName, userId);
                    return Result<bool>.Fail("Failed to log admin operation", 500);
                }

                await _unitOfWork.CommitAsync();
                await transaction.CommitAsync();

                _logger.LogInformation("Transaction committed successfully for role {Operation} of user {UserId}.", operationName, userId);
            }
            catch (Exception e)
            {
                await transaction.RollbackAsync();
                _logger.LogError(e, "Exception occurred while trying to {Operation} role '{Role}' for user {UserId}",
                    operationName, role, userId);

                return Result<bool>.Fail("An error occurred while committing the transaction", 500);
            }

            _logger.LogInformation("Role '{Role}' successfully {Operation} for user '{UserId}' by admin '{AdminId}'",
                role, operationName, userId, adminId);

            return Result<bool>.Ok(true);
        }

        #endregion

        #region Public Methods

        public async Task<Result<List<string>>> GetAllRolesAsync()
        {
            _logger.LogInformation("Fetching all roles from the database...");

            var roles = await _roleManager.Roles.Select(r => r.Name).ToListAsync();

            if (roles == null || roles.Count == 0)
            {
                _logger.LogWarning("No roles found in the database.");
                return Result<List<string>>.Fail("No roles found", 404);
            }

            _logger.LogInformation("Retrieved {Count} roles: {Roles}", roles.Count, string.Join(", ", roles));
            return Result<List<string>>.Ok(roles);
        }

        public async Task<Result<bool>> AddRoleToUserAsync(string userId, string role, string adminId)
        {
            _logger.LogInformation("Admin {AdminId} attempting to add role '{Role}' to user {UserId}", adminId, role, userId);

            var validation = await ValidateIdsAndRole(userId, adminId, role);
            if (!validation.Success)
            {
                _logger.LogWarning("AddRoleToUserAsync validation failed for AdminId: {AdminId}, UserId: {UserId}", adminId, userId);
                return validation;
            }

            var user = await _userManager.FindByIdAsync(userId);
            if (user == null || user.DeletedAt != null)
            {
                _logger.LogWarning("User {UserId} not found or deleted while adding role '{Role}'.", userId, role);
                return Result<bool>.Fail("User not found", 404);
            }

            _logger.LogInformation("Proceeding to add role '{Role}' for user {UserId}", role, userId);

            return await ExecuteRoleChangeAsync(
                "add",
                () => _userManager.AddToRoleAsync(user, role),
                $"Added role '{role}' to user {user.UserName} (ID: {user.Id}) by AdminId({adminId})",
                userId,
                role,
                adminId,
				Opreations.UpdateOpreation
            );
        }

        public async Task<Result<bool>> RemoveRoleFromUserAsync(string userId, string role, string adminId)
        {
            _logger.LogInformation("Admin {AdminId} attempting to remove role '{Role}' from user {UserId}", adminId, role, userId);

            var validation = await ValidateIdsAndRole(userId, adminId, role);
            if (!validation.Success)
            {
                _logger.LogWarning("RemoveRoleFromUserAsync validation failed for AdminId: {AdminId}, UserId: {UserId}", adminId, userId);
                return validation;
            }

            var user = await _userManager.FindByIdAsync(userId);
            if (user == null || user.DeletedAt != null)
            {
                _logger.LogWarning("User {UserId} not found or deleted while removing role '{Role}'.", userId, role);
                return Result<bool>.Fail("User not found", 404);
            }

            if (!await _userManager.IsInRoleAsync(user, role))
            {
                _logger.LogWarning("User {UserId} is not assigned to role '{Role}'. Cannot remove.", userId, role);
                return Result<bool>.Fail("User is not assigned to this role", 400);
            }

            _logger.LogInformation("Proceeding to remove role '{Role}' from user {UserId}", role, userId);

            return await ExecuteRoleChangeAsync(
                "remove",
                () => _userManager.RemoveFromRoleAsync(user, role),
                $"Removed role '{role}' from user {user.UserName} (ID: {user.Id}) by AdminId({adminId})",
                userId,
                role,
                adminId,
				Opreations.UpdateOpreation
            );
        }

        #endregion
    }
}


