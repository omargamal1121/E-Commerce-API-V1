using E_Commerce.Models;
using E_Commerce.Services.AdminOperationServices;
using E_Commerce.UOW;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Threading.Tasks;

namespace E_Commerce.Services.AccountServices.UserMangment
{
    public class UserRoleMangementService: IUserRoleMangementService
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
            _logger = logger;
            _userManager = userManager;
            _roleManager = roleManager;
        }
        public async Task<Result<List<string>>> GetAllRolesAsync()
        {
            _logger.LogInformation("Fetching all roles from the database");

            var roles = await _roleManager.Roles.Select(r => r.Name).ToListAsync();

            if (roles == null || roles.Count == 0)
            {
                _logger.LogWarning("No roles found in the database.");
                return Result<List<string>>.Fail("No roles found", 404);
            }

            _logger.LogInformation("Retrieved {Count} roles from the database: {Roles}", roles.Count, string.Join(", ", roles));
            return Result<List<string>>.Ok(roles);
        }


        public async Task<Result<bool>> AddRoleToUserAsync(string id, string role)
        {
            _logger.LogInformation("Attempting to add role '{Role}' to user with Id '{UserId}'", role, id);

            var user = await _userManager.FindByIdAsync(id);
            if (user == null)
            {
                _logger.LogWarning("User with Id '{UserId}' not found.", id);
                return Result<bool>.Fail("Can't find User id", 404);
            }

            if (string.IsNullOrWhiteSpace(role))
            {
                _logger.LogWarning("Invalid role name provided for user '{UserId}'", id);
                return Result<bool>.Fail("Invalid role", 400);
            }

            if (!await _roleManager.RoleExistsAsync(role))
            {
                _logger.LogWarning("Role '{Role}' does not exist. User '{UserId}' not updated.", role, id);
                return Result<bool>.Fail("Role does not exist", 400);
            }
            using var transaction = await _unitOfWork.BeginTransactionAsync();
            var result = await _userManager.AddToRoleAsync(user, role);
            if (!result.Succeeded)
            {
                await transaction.RollbackAsync();
                var errors = string.Join(", ", result.Errors.Select(e => e.Description));
                _logger.LogError("Failed to add role '{Role}' to user '{UserId}'. Errors: {Errors}", role, id, errors);
                return Result<bool>.Fail(errors, 400);
            }
            var isadded = await _adminOpreationServices.AddAdminOpreationAsync("Add Role to User" + $"Added role '{role}' to user {user.UserName} (ID: {user.Id})", Enums.Opreations.UpdateOpreation, id, null);
            if (!isadded.Success)
            {
                await transaction.RollbackAsync();
                _logger.LogError("Failed to log admin operation for adding role '{Role}' to user '{UserId}'", role, id);
                return Result<bool>.Fail("Failed to log admin operation", 500);
            }
            await transaction.CommitAsync();
            await _unitOfWork.CommitAsync();

            _logger.LogInformation("Role '{Role}' successfully added to user '{UserId}'", role, id);
            return Result<bool>.Ok(true);
        }
        public async Task<Result<bool>> RemoveRoleFromUserAsync(string id, string role)
        {
            _logger.LogInformation("Attempting to remove role '{Role}' from user with Id '{UserId}'", role, id);

            var user = await _userManager.FindByIdAsync(id);
            if (user == null)
            {
                _logger.LogWarning("User with Id '{UserId}' not found.", id);
                return Result<bool>.Fail("Can't find User id", 404);
            }

            if (string.IsNullOrWhiteSpace(role))
            {
                _logger.LogWarning("Invalid role name provided for user '{UserId}'", id);
                return Result<bool>.Fail("Invalid role", 400);
            }

            if (!await _roleManager.RoleExistsAsync(role))
            {
                _logger.LogWarning("Role '{Role}' does not exist. Cannot remove from user '{UserId}'.", role, id);
                return Result<bool>.Fail("Role does not exist", 400);
            }

            if (!await _userManager.IsInRoleAsync(user, role))
            {
                _logger.LogWarning("User '{UserId}' is not in role '{Role}', nothing to remove.", id, role);
                return Result<bool>.Fail("User is not assigned to this role", 400);
            }
            using var transaction = await _unitOfWork.BeginTransactionAsync();

            var result = await _userManager.RemoveFromRoleAsync(user, role);
            if (!result.Succeeded)
            {
                await transaction.RollbackAsync();
                var errors = string.Join(", ", result.Errors.Select(e => e.Description));
                _logger.LogError("Failed to remove role '{Role}' from user '{UserId}'. Errors: {Errors}", role, id, errors);
                return Result<bool>.Fail(errors, 400);
            }
            var isadded = await _adminOpreationServices.AddAdminOpreationAsync("Remove Role from User" + $"Removed role '{role}' from user {user.UserName} (ID: {user.Id})", Enums.Opreations.UpdateOpreation, id, null);
            if (!isadded.Success)
            {
                await transaction.RollbackAsync();
                _logger.LogError("Failed to log admin operation for removing role '{Role}' from user '{UserId}'", role, id);
                return Result<bool>.Fail("Failed to log admin operation", 500);
            }
            await transaction.CommitAsync();
            await _unitOfWork.CommitAsync();


            _logger.LogInformation("Role '{Role}' successfully removed from user '{UserId}'", role, id);
            return Result<bool>.Ok(true);
        }

    }
}
