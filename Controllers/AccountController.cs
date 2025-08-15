using E_Commerce.DtoModels;
using E_Commerce.DtoModels.AccountDtos;
using E_Commerce.DtoModels.Responses;
using E_Commerce.DtoModels.TokenDtos;
using E_Commerce.ErrorHnadling;
using E_Commerce.Interfaces;
using E_Commerce.Services;
using E_Commerce.Services.AccountServices.AccountManagement;
using E_Commerce.Services.AccountServices.Authentication;
using E_Commerce.Services.AccountServices.Password;
using E_Commerce.Services.AccountServices.Profile;
using E_Commerce.Services.AccountServices.Registration;
using E_Commerce.Services.AccountServices.Shared;
using E_Commerce.Services.EmailServices;
using Hangfire;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.IdentityModel.Tokens;
using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using System.Text;

namespace E_Commerce.Controllers
{
	/// <summary>
	/// Controller for handling user account operations
	/// </summary>
	[Route("api/[controller]")]
	[ApiController]
	public class AccountController : ControllerBase
	{
		private readonly ILogger<AccountController> _logger;
		private readonly IAuthenticationService _authenticationService;
		private readonly IRegistrationService _registrationService;
		private readonly IProfileService _profileService;
		private readonly IPasswordService _passwordService;
		private readonly IAccountManagementService _accountManagementService;
		private readonly IBackgroundJobClient _backgroundJobClient;
		private readonly IAccountLinkBuilder _linkBuilder;
		private readonly IErrorNotificationService _errorNotificationService;

		public AccountController(
			IBackgroundJobClient backgroundJobClient,
			IAccountLinkBuilder linkBuilder,
			IAuthenticationService authenticationService,
			IRegistrationService registrationService,
			IProfileService profileService,
			IPasswordService passwordService,
			IAccountManagementService accountManagementService,
			ILogger<AccountController> logger,
			IErrorNotificationService errorNotificationService)
		{
			_backgroundJobClient = backgroundJobClient ?? throw new ArgumentNullException(nameof(backgroundJobClient));
			_linkBuilder = linkBuilder ?? throw new ArgumentNullException(nameof(linkBuilder));
			_authenticationService = authenticationService ?? throw new ArgumentNullException(nameof(authenticationService));
			_registrationService = registrationService ?? throw new ArgumentNullException(nameof(registrationService));
			_profileService = profileService ?? throw new ArgumentNullException(nameof(profileService));
			_passwordService = passwordService ?? throw new ArgumentNullException(nameof(passwordService));
			_accountManagementService = accountManagementService ?? throw new ArgumentNullException(nameof(accountManagementService));
			_logger = logger ?? throw new ArgumentNullException(nameof(logger));
			_errorNotificationService = errorNotificationService ?? throw new ArgumentNullException(nameof(errorNotificationService));
		}

		/// <summary>
		/// Authenticates a user and returns JWT tokens
		/// </summary>
		[EnableRateLimiting("login")]
		[HttpPost("login")]
		[ActionName(nameof(LoginAsync))]
		[ProducesResponseType(typeof(ApiResponse<TokensDto>), StatusCodes.Status200OK)]
		[ProducesResponseType(typeof(ApiResponse<string>), StatusCodes.Status400BadRequest)]
		[ProducesResponseType(typeof(ApiResponse<string>), StatusCodes.Status401Unauthorized)]
		[ProducesResponseType(typeof(ApiResponse<string>), StatusCodes.Status500InternalServerError)]
		public async Task<ActionResult<ApiResponse<TokensDto>>> LoginAsync([FromBody] LoginDTo login)
		{
			try
			{
				if (!ModelState.IsValid)
				{
					var errors = GetModelErrors();
					_logger.LogWarning($"ModelState errors: {string.Join(", ", errors)}");
					return BadRequest(ApiResponse<string>.CreateErrorResponse("Invalid Data", new ErrorResponse("Invalid Data", errors), 400));
				}

				_logger.LogInformation($"In {nameof(LoginAsync)} Method ");
				var result = await _authenticationService.LoginAsync(login.Email, login.Password);
				return HandleResult<TokensDto>(result, nameof(LoginAsync));
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, $"Error in {nameof(LoginAsync)}");
					_backgroundJobClient.Enqueue(()=> _errorNotificationService.SendErrorNotificationAsync(ex.Message, ex.StackTrace));
				return StatusCode(500, ApiResponse<TokensDto>.CreateErrorResponse("Server Error", new ErrorResponse("Server Error", "An unexpected error occurred during login."), 500));
			}
		}

		/// <summary>
		/// Registers a new user account
		/// </summary>
		[EnableRateLimiting("register")]
		[HttpPost("register")]
		[ActionName(nameof(RegisterAsync))]
		[ProducesResponseType(typeof(ApiResponse<RegisterResponse>), StatusCodes.Status201Created)]
		[ProducesResponseType(typeof(ApiResponse<string>), StatusCodes.Status400BadRequest)]
		[ProducesResponseType(typeof(ApiResponse<string>), StatusCodes.Status409Conflict)]
		[ProducesResponseType(typeof(ApiResponse<string>), StatusCodes.Status500InternalServerError)]
		public async Task<ActionResult<ApiResponse<RegisterResponse>>> RegisterAsync([FromBody] RegisterDto usermodel)
		{
			try
			{
				_logger.LogInformation($"In {nameof(RegisterAsync)} Method ");
				if (!ModelState.IsValid)
				{
					var errors = GetModelErrors();
					_logger.LogWarning($"ModelState errors: {string.Join(", ", errors)}");
					return BadRequest(ApiResponse<RegisterResponse>.CreateErrorResponse("Invalid Data", new ErrorResponse($"Invalid Data", errors), 400));
				}

				var result = await _registrationService.RegisterAsync(usermodel);
				return HandleResult<RegisterResponse>(result, actionName: nameof(RegisterAsync));
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, $"Error in {nameof(RegisterAsync)}");
					_backgroundJobClient.Enqueue(()=> _errorNotificationService.SendErrorNotificationAsync(ex.Message, ex.StackTrace));
				return StatusCode(500, ApiResponse<RegisterResponse>.CreateErrorResponse("Server Error", new ErrorResponse("Server Error", "An unexpected error occurred during registration."), 500));
			}
		}
	
	
		[ActionName(nameof(GetProfileAsync))]
		[ProducesResponseType(typeof(ApiResponse<ProfileDto>), StatusCodes.Status200OK)]
		[HttpGet]
		[ProducesResponseType(typeof(ApiResponse<ProfileDto>), StatusCodes.Status500InternalServerError)]
		[Authorize]
		public async Task<ActionResult<ApiResponse<ProfileDto>>> GetProfileAsync()
		{
			try
			{

				_logger.LogInformation($"In {nameof(GetProfileAsync)} Method ");
				string userid = HttpContext.Items["UserId"].ToString();
				
				var result = await _profileService.GetProfileAsync(userid);
				return HandleResult<ProfileDto>(result, actionName: nameof(RegisterAsync));
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, $"Error in {nameof(RegisterAsync)}");
					_backgroundJobClient.Enqueue(()=> _errorNotificationService.SendErrorNotificationAsync(ex.Message, ex.StackTrace));
				return StatusCode(500, ApiResponse<ProfileDto>.CreateErrorResponse("Server Error", new ErrorResponse("Server Error", "An unexpected error occurred during registration."), 500));
			}
		}

		/// <summary>
		/// Refreshes the JWT token using a refresh token
		/// </summary>
		[HttpGet("refresh-token")]
		[ActionName(nameof(RefreshTokenAsync))]
		[ProducesResponseType(typeof(ApiResponse<TokensDto>), StatusCodes.Status200OK)]
		[ProducesResponseType(typeof(ApiResponse<TokensDto>), StatusCodes.Status400BadRequest)]
		[ProducesResponseType(typeof(ApiResponse<TokensDto>), StatusCodes.Status401Unauthorized)]
		[ProducesResponseType(typeof(ApiResponse<TokensDto>), StatusCodes.Status500InternalServerError)]
		public async Task<ActionResult<ApiResponse<TokensDto>>> RefreshTokenAsync()
		{
			try
			{
				_logger.LogInformation($"In {nameof(RefreshTokenAsync)} Method");
				if (!ModelState.IsValid)
				{
					var errors = GetModelErrors();
					_logger.LogWarning($"ModelState errors: {string.Join(", ", errors)}");
					return BadRequest(ApiResponse<string>.CreateErrorResponse("Invalid Data", new ErrorResponse("Invalid Data", errors), 400));
				}

				var result = await _authenticationService.RefreshTokenAsync();
				return HandleResult<TokensDto>(result, nameof(RefreshTokenAsync));
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, $"Error in {nameof(RefreshTokenAsync)}");
					_backgroundJobClient.Enqueue(()=> _errorNotificationService.SendErrorNotificationAsync(ex.Message, ex.StackTrace));
				return StatusCode(500, ApiResponse<TokensDto>.CreateErrorResponse("Server Error", new ErrorResponse("Server Error", "An unexpected error occurred during token refresh."), 500));
			}
		}

		/// <summary>
		/// Changes the user's password
		/// </summary>
		[HttpPatch("change-password")]
		[ActionName(nameof(ChangePasswordAsync))]
		[Authorize]
		[ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
		[ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status400BadRequest)]
		[ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status401Unauthorized)]
		[ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status500InternalServerError)]
		public async Task<ActionResult<ApiResponse<bool>>> ChangePasswordAsync([FromBody] ChangePasswordDto model)
		{
			try
			{
				_logger.LogInformation($"In {nameof(ChangePasswordAsync)} Method");
				if (!ModelState.IsValid)
				{
					var errors = GetModelErrors();
					_logger.LogWarning($"ModelState errors: {string.Join(", ", errors)}");
					return BadRequest(ApiResponse<bool>.CreateErrorResponse("Invalid Data", new ErrorResponse("Invalid Data", errors), 400));
				}

				string? userid = GetIdFromToken();
				if (userid.IsNullOrEmpty())
				{
					_logger.LogError("Can't find userid in token");
					return Unauthorized(ApiResponse<bool>.CreateErrorResponse("Authorization", new ErrorResponse("Authorization", "Can't find userid in token"), 401));
				}

				var result = await _passwordService.ChangePasswordAsync(userid, model.CurrentPass, model.NewPass);
				return HandleResult<bool>(result, nameof(ChangePasswordAsync));
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, $"Error in {nameof(ChangePasswordAsync)}");
					_backgroundJobClient.Enqueue(()=> _errorNotificationService.SendErrorNotificationAsync(ex.Message, ex.StackTrace));
				return StatusCode(500, ApiResponse<bool>.CreateErrorResponse("Server Error", new ErrorResponse("Server Error", "An unexpected error occurred during password change."), 500));
			}
		}

		/// <summary>
		/// Changes the user's email address
		/// </summary>
		[Authorize]
		[HttpPatch("change-email")]
		[ActionName(nameof(ChangeEmailAsync))]
		[ProducesResponseType(typeof(ApiResponse<ChangeEmailResultDto>), StatusCodes.Status200OK)]
		[ProducesResponseType(typeof(ApiResponse<string>), StatusCodes.Status400BadRequest)]
		[ProducesResponseType(typeof(ApiResponse<string>), StatusCodes.Status401Unauthorized)]
		[ProducesResponseType(typeof(ApiResponse<string>), StatusCodes.Status409Conflict)]
		[ProducesResponseType(typeof(ApiResponse<string>), StatusCodes.Status500InternalServerError)]
		public async Task<ActionResult<ApiResponse<ChangeEmailResultDto>>> ChangeEmailAsync( string NewEmail)
		{
			try
			{
				if (!ModelState.IsValid)
				{
					var errors = GetModelErrors();
					_logger.LogWarning($"ModelState errors: {string.Join(", ", errors)}");
					return BadRequest(ApiResponse<string>.CreateErrorResponse("Invalid Data", new ErrorResponse("Invalid Data", $"errors:{string.Join(", ", errors)}"), 400));
				}
				string userid=HttpContext
					.Items["UserId"]?.ToString();
				_logger.LogInformation($"In {nameof(ChangeEmailAsync)} Method");
				var result = await _profileService.ChangeEmailAsync(NewEmail, userid);
				return HandleResult<ChangeEmailResultDto>(result, nameof(ChangeEmailAsync)) ;
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, $"Error in {nameof(ChangeEmailAsync)}");
					_backgroundJobClient.Enqueue(()=> _errorNotificationService.SendErrorNotificationAsync(ex.Message, ex.StackTrace));
				return StatusCode(500, ApiResponse<ChangeEmailResultDto>.CreateErrorResponse("Server Error", new ErrorResponse("Server Error", "An unexpected error occurred during email change."), 500));
			}
		}

		/// <summary>
		/// Logs out the current user
		/// </summary>
		[Authorize]
		[HttpGet("logout")]
		[ActionName(nameof(LogoutAsync))]
		[ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
		[ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status401Unauthorized)]
		[ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status500InternalServerError)]
		public async Task<ActionResult<ApiResponse<bool>>> LogoutAsync()
		{
			try
			{
				_logger.LogInformation($"In {nameof(LogoutAsync)} Method");

				string? userid = GetIdFromToken();
				if (userid.IsNullOrEmpty())
				{
					_logger.LogError("Can't find userid in token");
					return Unauthorized(ApiResponse<string>.CreateErrorResponse("Authorization", new ErrorResponse("Authorization", "Can't find userid in token"), 401));
				}

				var result = await _authenticationService.LogoutAsync(userid);
				return HandleResult<bool>(result, nameof(LogoutAsync));
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, $"Error in {nameof(LogoutAsync)}");
					_backgroundJobClient.Enqueue(()=> _errorNotificationService.SendErrorNotificationAsync(ex.Message, ex.StackTrace));
				return StatusCode(500, ApiResponse<bool>.CreateErrorResponse("Server Error", new ErrorResponse("Server Error", "An unexpected error occurred during logout."), 500));
			}
		}

		/// <summary>
		/// Deletes the current user's account
		/// </summary>
		[Authorize]
		[HttpDelete("delete-account")]
		[ActionName(nameof(DeleteAsync))]
		[ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
		[ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status401Unauthorized)]
		[ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status500InternalServerError)]
		public async Task<ActionResult<ApiResponse<bool>>> DeleteAsync()
		{
			try
			{
				_logger.LogInformation($"In {nameof(DeleteAsync)} Method");
				string? userid = GetIdFromToken();
				if (userid.IsNullOrEmpty())
				{
					_logger.LogError("Can't Get Userid from token");
					return Unauthorized(ApiResponse<bool>.CreateErrorResponse("Authorization", new ErrorResponse("Authorization", "Can't found userid in token"), 401));
				}
				var result = await _accountManagementService.DeleteAsync(userid);
				return HandleResult<bool>(result, nameof(DeleteAsync));
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, $"Error in {nameof(DeleteAsync)}");
					_backgroundJobClient.Enqueue(()=> _errorNotificationService.SendErrorNotificationAsync(ex.Message, ex.StackTrace));
				return StatusCode(500, ApiResponse<bool>.CreateErrorResponse("Server Error", new ErrorResponse("Server Error", "An unexpected error occurred during account deletion."), 500));
			}
		}

		/// <summary>
		/// Uploads a profile photo for the current user
		/// </summary>
		[Authorize]
		[HttpPatch("upload-photo")]
		[ActionName(nameof(UploadPhotoAsync))]
		[ProducesResponseType(typeof(ApiResponse<UploadPhotoResponseDto>), StatusCodes.Status200OK)]
		[ProducesResponseType(typeof(ApiResponse<string>), StatusCodes.Status400BadRequest)]
		[ProducesResponseType(typeof(ApiResponse<string>), StatusCodes.Status401Unauthorized)]
		[ProducesResponseType(typeof(ApiResponse<string>), StatusCodes.Status500InternalServerError)]
		public async Task<ActionResult<ApiResponse<UploadPhotoResponseDto>>> UploadPhotoAsync([FromForm] UploadPhotoDto image)
		{
			try
			{
				if (!ModelState.IsValid)
				{
					var errors = GetModelErrors();
					_logger.LogWarning($"ModelState errors: {string.Join(", ", errors)}");
					return BadRequest(ApiResponse<string>.CreateErrorResponse("Invalid Data", new ErrorResponse("Invalid Data", $"errors:{string.Join(", ", errors)}"), 400));
				}

				_logger.LogInformation($"Executing {nameof(UploadPhotoAsync)}");
				string? id = GetIdFromToken();
				if (id.IsNullOrEmpty())
				{
					_logger.LogError("Can't find userid in token");
					return Unauthorized(ApiResponse<string>.CreateErrorResponse("Authorization", new ErrorResponse("Authorization", "Can't find userid in token"), 401));
				}

				var result = await _profileService.UploadPhotoAsync(image.image, id);
				return HandleResult<UploadPhotoResponseDto>(result, nameof(UploadPhotoAsync));
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, $"Error in {nameof(UploadPhotoAsync)}");
					_backgroundJobClient.Enqueue(()=> _errorNotificationService.SendErrorNotificationAsync(ex.Message, ex.StackTrace));
				return StatusCode(500, ApiResponse<UploadPhotoResponseDto>.CreateErrorResponse("Server Error", new ErrorResponse("Server Error", "An unexpected error occurred during photo upload."), 500));
			}
		}

		/// <summary>
		/// Confirms a user's email address
		/// </summary>
		[HttpGet("confirm-email")]
		[ActionName(nameof(ConfirmEmailAsync))]
		[ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
		[ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status400BadRequest)]
		[ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status404NotFound)]
		[ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status500InternalServerError)]
		public async Task<ActionResult<ApiResponse<bool>>> ConfirmEmailAsync(string userId, string token)
		{
			try
			{
				_logger.LogInformation($"Executing {nameof(ConfirmEmailAsync)}");
				if (!ModelState.IsValid)
				{
					var errors = GetModelErrors();
					_logger.LogWarning($"ModelState errors: {string.Join(", ", errors)}");
					return BadRequest(ApiResponse<bool>.CreateErrorResponse("Invalid Data", new ErrorResponse("Invalid Data", $"errors:{string.Join(", ", errors)}"), 400));
				}
				var decodedToken = Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(token));

				var result = await _registrationService.ConfirmEmailAsync(userId, decodedToken);
				return HandleResult<bool>(result, nameof(ConfirmEmailAsync));
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, $"Error in {nameof(ConfirmEmailAsync)}");
					_backgroundJobClient.Enqueue(()=> _errorNotificationService.SendErrorNotificationAsync(ex.Message, ex.StackTrace));
				return StatusCode(500, ApiResponse<bool>.CreateErrorResponse("Server Error", new ErrorResponse("Server Error", "An unexpected error occurred during email confirmation."), 500));
			}
		}

		/// <summary>
		/// Resends the email confirmation link
		/// </summary>
		[HttpGet("resend-confirmation-email")]
		[ActionName(nameof(ResendConfirmationEmailAsync))]
		[ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
		[ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status400BadRequest)]
		[ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status404NotFound)]
		[ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status500InternalServerError)]
		public async Task<ActionResult<ApiResponse<bool>>> ResendConfirmationEmailAsync( string Email)
		{
			try
			{
				_logger.LogInformation($"Executing {nameof(ResendConfirmationEmailAsync)}");
				if (!ModelState.IsValid)
				{
					var errors = GetModelErrors();
					_logger.LogWarning($"ModelState errors: {string.Join(", ", errors)}");
					return BadRequest(ApiResponse<bool>.CreateErrorResponse("Invalid Data", new ErrorResponse("Invalid Data", $"errors:{string.Join(", ", errors)}"), 400));
				}

				var result = await _registrationService.ResendConfirmationEmailAsync(Email);
				return HandleResult<bool>(result, nameof(ResendConfirmationEmailAsync));
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, $"Error in {nameof(ResendConfirmationEmailAsync)}");
					_backgroundJobClient.Enqueue(()=> _errorNotificationService.SendErrorNotificationAsync(ex.Message, ex.StackTrace));
				return StatusCode(500, ApiResponse<bool>.CreateErrorResponse("Server Error", new ErrorResponse("Server Error", "An unexpected error occurred while resending confirmation email."), 500));
			}
		}

		/// <summary>
		/// Requests a password reset by sending a reset token to the user's email
		/// </summary>
		[EnableRateLimiting("reset")]
		[HttpGet("request-password-reset")]
		[ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
		[ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status400BadRequest)]
		public async Task<ActionResult<ApiResponse<bool>>> RequestPasswordReset(string Email)
		{
			try
			{
				if (!ModelState.IsValid)
				{
					var errors = GetModelErrors();
					return BadRequest(ApiResponse<string>.CreateErrorResponse("Invalid Data", new ErrorResponse("Invalid Data", errors), 400));
				}
				var result = await _passwordService.RequestPasswordResetAsync(Email);
				return HandleResult<bool>(result, nameof(RequestPasswordReset));
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, $"Error in {nameof(RequestPasswordReset)}");
					_backgroundJobClient.Enqueue(()=> _errorNotificationService.SendErrorNotificationAsync(ex.Message, ex.StackTrace));
				return StatusCode(500, ApiResponse<bool>.CreateErrorResponse("Server Error", new ErrorResponse("Server Error", "An unexpected error occurred while requesting password reset."), 500));
			}
		}

		/// <summary>
		/// Resets the user's password using a reset token
		/// </summary>
		[EnableRateLimiting("reset")]
		[HttpPost("reset-password")]
		[ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
		[ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status400BadRequest)]
		public async Task<ActionResult<ApiResponse<bool>>> ResetPassword([FromBody] ResetPasswordDto dto)
		{
			try
			{
				if (!ModelState.IsValid)
				{
					var errors = GetModelErrors();
					return BadRequest(ApiResponse<bool>.CreateErrorResponse("Invalid Data", new ErrorResponse("Invalid Data", errors), 400));
				}
				var result = await _passwordService.ResetPasswordAsync(dto.Email, dto.Token, dto.NewPassword);
				return HandleResult<bool>(result, nameof(ResetPassword));
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, $"Error in {nameof(ResetPassword)}");
					_backgroundJobClient.Enqueue(()=> _errorNotificationService.SendErrorNotificationAsync(ex.Message, ex.StackTrace));
				return StatusCode(500, ApiResponse<bool>.CreateErrorResponse("Server Error", new ErrorResponse("Server Error", "An unexpected error occurred while resetting password."), 500));
			}
		}

		private string? GetIdFromToken()
		{
			return HttpContext.Items["UserId"]?.ToString();
		}

		private List<string> GetModelErrors()
		{
			return ModelState.SelectMany(x => x.Value.Errors.Select(e => e.ErrorMessage)).ToList();
		}

		private ActionResult<ApiResponse<T>> HandleResult<T>(Result<T> result, string? actionName = null, int? id = null)
		{

			var linkes= _linkBuilder.GenerateLinks(id);
			var apiResponse = result.Success
				? ApiResponse<T>.CreateSuccessResponse(result.Message, result.Data, result.StatusCode, warnings: result.Warnings,links: linkes)
				: ApiResponse<T>.CreateErrorResponse(result.Message, new ErrorResponse("Error", result.Message), result.StatusCode, warnings: result.Warnings,links: linkes);

			switch (result.StatusCode)
			{
				case 200:
					return Ok(apiResponse);
				case 201:
					return actionName != null && id.HasValue ? CreatedAtAction(actionName, new { id }, apiResponse) : StatusCode(201, apiResponse);
				case 400:
					return BadRequest(apiResponse);
				case 401:
					return Unauthorized(apiResponse);
				case 404:
					return NotFound(apiResponse);
				case 409:
					return Conflict(apiResponse);
				default:
					return StatusCode(result.StatusCode, apiResponse);
			}
		}

	}
}
