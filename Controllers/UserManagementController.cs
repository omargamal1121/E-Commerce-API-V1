using E_Commerce.DtoModels.Responses;
using E_Commerce.ErrorHnadling;
using E_Commerce.Services.AccountServices.UserMangment;
using E_Commerce.Services.EmailServices;
using Hangfire;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations;
using static E_Commerce.Services.AccountServices.UserMangment.UserQueryServiece;

namespace E_Commerce.Controllers
{
	[Route("api/[controller]")]
	[ApiController]
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
            IUserAccountManagementService userAccountManagementService
)
		{
            _errorNotificationService = errorNotificationService;
            _backgroundJobClient = backgroundJobClient;
            _logger = logger;
            _userQueryServiece = userQueryServiece;
            _userAccountManagementService = userAccountManagementService;
            _userRoleMangementService = userRoleMangementService;


        }

		[HttpGet("users")]
        [ActionName(nameof(GetUsersAsync))]
        [ProducesResponseType(typeof(ApiResponse<List<Userdto>>), StatusCodes.Status200OK)]
        [Authorize(Roles = "SuperAdmin")]


        public ActionResult<ApiResponse<List<Userdto>>> GetUsersAsync(
            [FromQuery] string? name = null,
            [FromQuery] string? email = null,
            [FromQuery] string? role = null,
            [FromQuery] string? phonenumber = null,
            [FromQuery] bool? IsActive = null,
            [FromQuery] bool? isDeleted = null,
            [FromQuery, Range(1, int.MaxValue)] int page = 1,
            [FromQuery, Range(1, 100)] int pageSize = 10)
        {
            try
            {
                _logger.LogInformation($"In {nameof(GetUsersAsync)} Method ");
                if (!ModelState.IsValid)
                {
                    var errors = GetModelErrors();
                    _logger.LogWarning($"ModelState errors: {string.Join(", ", errors)}");
                    return BadRequest(ApiResponse<string>.CreateErrorResponse("Invalid Data", new ErrorResponse("Invalid Data", errors), 400));
                }
            
                var result = _userQueryServiece.FilterUsers(name, email, role, phonenumber, IsActive, isDeleted, page, pageSize);
                return HandleResult<List<Userdto>>(result, nameof(GetUsersAsync));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error in {nameof(GetUsersAsync)}");
                _backgroundJobClient.Enqueue(() => _errorNotificationService.SendErrorNotificationAsync(ex.Message, ex.StackTrace));
                return StatusCode(500, ApiResponse<string>.CreateErrorResponse("Server Error", new ErrorResponse("Server Error", "An unexpected error occurred while retrieving users."), 500));
            }
        }
        [HttpGet("user/{id}")]
        [ActionName(nameof(GetUserByIdAsync))]
        [ProducesResponseType(typeof(ApiResponse<Userdto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<string>), StatusCodes.Status404NotFound)]
        [Authorize(Roles = "SuperAdmin")]
        public async Task<ActionResult<ApiResponse<Userdto>>> GetUserByIdAsync(string id)
        {

            _logger.LogInformation($"In {nameof(GetUsersAsync)} Method ");
         
            var result = await _userQueryServiece.GetUserByIdAsnyc(id);
            return HandleResult<Userdto>(result, nameof(GetUserByIdAsync));
        }
        [HttpGet("roles")]
        [ActionName(nameof(GetAllRolesAsync))]
        [ProducesResponseType(typeof(ApiResponse<List<string>>), StatusCodes.Status200OK)]
        [Authorize(Roles = "SuperAdmin")]
        public async Task<ActionResult<ApiResponse<List<string>>>> GetAllRolesAsync()
        {
            _logger.LogInformation($"In {nameof(GetUsersAsync)} Method ");
     

            var result = await _userRoleMangementService.GetAllRolesAsync();

            return HandleResult<List<string>>(result, nameof(GetAllRolesAsync));

        }
        [HttpPatch("add-role/{id}")]
        [ActionName(nameof(AddRoleToUserAsync))]
        [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<string>), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiResponse<string>), StatusCodes.Status404NotFound)]
        [Authorize(Roles = "SuperAdmin")]
        public async Task<ActionResult<ApiResponse<bool>>> AddRoleToUserAsync(string id, string role)
        {
            _logger.LogInformation($"In {nameof(GetUsersAsync)} Method ");
          

            var result = await _userRoleMangementService.AddRoleToUserAsync(id, role);
            return HandleResult<bool>(result, nameof(AddRoleToUserAsync));
        }
        [HttpPatch("Remove-role/{id}")]
        [ActionName(nameof(AddRoleToUserAsync))]
        [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<string>), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiResponse<string>), StatusCodes.Status404NotFound)]
        [Authorize(Roles = "SuperAdmin")]
        public async Task<ActionResult<ApiResponse<bool>>> RemoveRoleToUserAsync(string id, string role)
        {
            _logger.LogInformation($"In {nameof(GetUsersAsync)} Method ");
          
            var result = await _userRoleMangementService.RemoveRoleFromUserAsync(id, role);
            return HandleResult<bool>(result, nameof(AddRoleToUserAsync));
        }
        [HttpPatch("lock-user/{id}")]
        [ActionName(nameof(LockUserAccountAsync))]
        [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]

        [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status404NotFound)]
        [Authorize(Roles = "SuperAdmin")]
        public async Task<ActionResult<ApiResponse<bool>>> LockUserAccountAsync(string id)
        {
            _logger.LogInformation($"In {nameof(LockUserAccountAsync)} Method ");

            var result = await _userAccountManagementService.LockUserAsync(id);
            return HandleResult<bool>(result, nameof(LockUserAccountAsync));

        }
        [HttpPatch("unlock-user/{id}")]
        [ActionName(nameof(UnlockUserAccountAsync))]
        [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status404NotFound)]
        [Authorize(Roles = "SuperAdmin")]
        public async Task<ActionResult<ApiResponse<bool>>> UnlockUserAccountAsync(string id)
        {
            _logger.LogInformation($"In {nameof(UnlockUserAccountAsync)} Method ");
            var result = await _userAccountManagementService.UnlockUserAsync(id);
            return HandleResult<bool>(result, nameof(UnlockUserAccountAsync));
        }
        [HttpDelete("delete-user/{id}")]
        [ActionName(nameof(DeleteUserAccountAsync))]
        [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status404NotFound)]
        [Authorize(Roles = "SuperAdmin")]
        public async Task<ActionResult<ApiResponse<bool>>> DeleteUserAccountAsync(string id)
        {
            _logger.LogInformation($"In {nameof(DeleteUserAccountAsync)} Method ");
            var result = await _userAccountManagementService.DeleteUserAsync(id);
            return HandleResult<bool>(result, nameof(DeleteUserAccountAsync));
        }

    }
}
