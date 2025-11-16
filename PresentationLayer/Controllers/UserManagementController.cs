using ApplicationLayer.DtoModels.AccountDtos;
using ApplicationLayer.DtoModels.Responses;
using ApplicationLayer.ErrorHnadling;
using ApplicationLayer.Services.AccountServices.UserMangment;
using ApplicationLayer.Services.EmailServices;

using Hangfire;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations;
using static ApplicationLayer.Services.AccountServices.UserMangment.UserQueryServiece;

namespace DomainLayer.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize(Roles = "SuperAdmin")]
    public class UserManagementController : BaseController
    {
        private readonly ILogger<UserManagementController> _logger;
        private readonly IUserQueryServiece _userQueryServiece;
        private readonly IUserRoleMangementService _userRoleMangementService;
        private readonly IUserAccountManagementService _userAccountManagementService;
        private readonly IErrorNotificationService _errorNotificationService;
        private readonly IBackgroundJobClient _backgroundJobClient;

        public UserManagementController(
            IErrorNotificationService errorNotificationService,
            IBackgroundJobClient backgroundJobClient,
            ILogger<UserManagementController> logger,
            IUserQueryServiece userQueryServiece,
            IUserRoleMangementService userRoleMangementService,
            IUserAccountManagementService userAccountManagementService)
        {
            _errorNotificationService = errorNotificationService;
            _backgroundJobClient = backgroundJobClient;
            _logger = logger;
            _userQueryServiece = userQueryServiece;
            _userAccountManagementService = userAccountManagementService;
            _userRoleMangementService = userRoleMangementService;
        }

        // ✅ GET: api/usermanagement/users
        [HttpGet("users")]
        [ActionName(nameof(GetUsersAsync))]
        [ProducesResponseType(typeof(ApiResponse<List<Userdto>>), StatusCodes.Status200OK)]
        public ActionResult<ApiResponse<List<Userdto>>> GetUsersAsync(
            [FromQuery] string? name = null,
            [FromQuery] string? email = null,
            [FromQuery] string? role = null,
            [FromQuery] string? phonenumber = null,
            [FromQuery] bool? isActive = null,
            [FromQuery] bool? isDeleted = null,
            [FromQuery, Range(1, int.MaxValue)] int page = 1,
            [FromQuery, Range(1, 100)] int pageSize = 10)
        {
            _logger.LogInformation("In {ActionName} Method", nameof(GetUsersAsync));

            try
            {
                var result = _userQueryServiece.FilterUsers(name, email, role, phonenumber, isActive, isDeleted, page, pageSize);
                return HandleResult<List<Userdto>>(result, nameof(GetUsersAsync));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in {ActionName}", nameof(GetUsersAsync));
                _backgroundJobClient.Enqueue(() => _errorNotificationService.SendErrorNotificationAsync(ex.Message, ex.StackTrace));
                return StatusCode(500, ApiResponse<string>.CreateErrorResponse("Server Error",
                    new ErrorResponse("Server Error", "An unexpected error occurred while retrieving users."), 500));
            }
        }

        // ✅ GET: api/usermanagement/user/{id}
        [HttpGet("user/{id}")]
        [ActionName(nameof(GetUserByIdAsync))]
        [ProducesResponseType(typeof(ApiResponse<UserwithAddressdto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<UserwithAddressdto>), StatusCodes.Status404NotFound)]
        public async Task<ActionResult<ApiResponse<UserwithAddressdto>>> GetUserByIdAsync(string id)
        {
            _logger.LogInformation("In {ActionName} Method", nameof(GetUserByIdAsync));

            var result = await _userQueryServiece.GetUserByIdAsnyc(id);
            return HandleResult<UserwithAddressdto>(result, nameof(GetUserByIdAsync));
        }

        // ✅ GET: api/usermanagement/roles
        [HttpGet("roles")]
        [ActionName(nameof(GetAllRolesAsync))]
        [ProducesResponseType(typeof(ApiResponse<List<string>>), StatusCodes.Status200OK)]
        public async Task<ActionResult<ApiResponse<List<string>>>> GetAllRolesAsync()
        {
            _logger.LogInformation("In {ActionName} Method", nameof(GetAllRolesAsync));

            var result = await _userRoleMangementService.GetAllRolesAsync();
            return HandleResult<List<string>>(result, nameof(GetAllRolesAsync));
        }

        // ✅ PATCH: api/usermanagement/add-role/{id}?role=Admin
        [HttpPatch("add-role/{id}")]
        [ActionName(nameof(AddRoleToUserAsync))]
        [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<string>), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiResponse<string>), StatusCodes.Status404NotFound)]
        public async Task<ActionResult<ApiResponse<bool>>> AddRoleToUserAsync(string id, [FromQuery] string role)
        {
            _logger.LogInformation("In {ActionName} Method", nameof(AddRoleToUserAsync));

            if (string.IsNullOrWhiteSpace(role))
            {
                return BadRequest(ApiResponse<string>.CreateErrorResponse("Invalid Role",
                    new ErrorResponse("Invalid Role","Role parameter is required"), 400));
            }

            var adminId = GetUserId();
            var result = await _userRoleMangementService.AddRoleToUserAsync(id, role, adminId);

            _logger.LogInformation("Admin {AdminId} executed {Action} on User {UserId}", adminId, nameof(AddRoleToUserAsync), id);
            return HandleResult<bool>(result, nameof(AddRoleToUserAsync));
        }

        // ✅ PATCH: api/usermanagement/remove-role/{id}?role=User
        [HttpPatch("remove-role/{id}")]
        [ActionName(nameof(RemoveRoleToUserAsync))]
        [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<string>), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiResponse<string>), StatusCodes.Status404NotFound)]
        public async Task<ActionResult<ApiResponse<bool>>> RemoveRoleToUserAsync(string id, [FromQuery] string role)
        {
            _logger.LogInformation("In {ActionName} Method", nameof(RemoveRoleToUserAsync));

            if (string.IsNullOrWhiteSpace(role))
            {
                return BadRequest(ApiResponse<string>.CreateErrorResponse("Error",
                    new ErrorResponse("Invalid Role", "Role parameter is required"), 400));
            }

            var adminId = GetUserId();
            var result = await _userRoleMangementService.RemoveRoleFromUserAsync(id, role, adminId);

            _logger.LogInformation("Admin {AdminId} executed {Action} on User {UserId}", adminId, nameof(RemoveRoleToUserAsync), id);
            return HandleResult<bool>(result, nameof(RemoveRoleToUserAsync));
        }

        // ✅ PATCH: api/usermanagement/lock-user/{id}
        [HttpPatch("lock-user/{id}")]
        [ActionName(nameof(LockUserAccountAsync))]
        [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status404NotFound)]
        public async Task<ActionResult<ApiResponse<bool>>> LockUserAccountAsync(string id)
        {
            _logger.LogInformation("In {ActionName} Method", nameof(LockUserAccountAsync));

            var adminId = GetUserId();
            _logger.LogInformation("Admin {AdminId} requested to lock user {UserId}", adminId, id);

            var result = await _userAccountManagementService.LockUserAsync(id, adminId);
            return HandleResult<bool>(result, nameof(LockUserAccountAsync));
        }

        // ✅ PATCH: api/usermanagement/unlock-user/{id}
        [HttpPatch("unlock-user/{id}")]
        [ActionName(nameof(UnlockUserAccountAsync))]
        [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status404NotFound)]
        public async Task<ActionResult<ApiResponse<bool>>> UnlockUserAccountAsync(string id)
        {
            _logger.LogInformation("In {ActionName} Method", nameof(UnlockUserAccountAsync));

            var adminId = GetUserId();
            _logger.LogInformation("Admin {AdminId} requested to unlock user {UserId}", adminId, id);

            var result = await _userAccountManagementService.UnlockUserAsync(id, adminId);
            return HandleResult<bool>(result, nameof(UnlockUserAccountAsync));
        }

        // ✅ DELETE: api/usermanagement/delete-user/{id}
        [HttpDelete("delete-user/{id}")]
        [ActionName(nameof(DeleteUserAccountAsync))]
        [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status404NotFound)]
        public async Task<ActionResult<ApiResponse<bool>>> DeleteUserAccountAsync(string id)
        {
            _logger.LogInformation("In {ActionName} Method", nameof(DeleteUserAccountAsync));

            var adminId = GetUserId();
            _logger.LogInformation("Admin {AdminId} requested to delete user {UserId}", adminId, id);

            var result = await _userAccountManagementService.DeleteUserAsync(id, adminId);
            return HandleResult<bool>(result, nameof(DeleteUserAccountAsync));
        }

        // ✅ PATCH: api/usermanagement/restore-user/{id}
        [HttpPatch("restore-user/{id}")]
        [ActionName(nameof(RestoreUserAccountAsync))]
        [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status404NotFound)]
        public async Task<ActionResult<ApiResponse<bool>>> RestoreUserAccountAsync(string id)
        {
            _logger.LogInformation("In {ActionName} Method", nameof(RestoreUserAccountAsync));

            var adminId = GetUserId();
            _logger.LogInformation("Admin {AdminId} requested to restore user {UserId}", adminId, id);

            var result = await _userAccountManagementService.RestoreUserAsync(id, adminId);
            return HandleResult<bool>(result, nameof(RestoreUserAccountAsync));
        }
    }
}
