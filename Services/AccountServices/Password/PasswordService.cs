using E_Commerce.DtoModels.Responses;
using E_Commerce.ErrorHnadling;
using E_Commerce.Interfaces;
using E_Commerce.Models;
using E_Commerce.Services.EmailServices;
using Hangfire;
using Microsoft.AspNetCore.Identity;

namespace E_Commerce.Services.AccountServices.Password
{
    public class PasswordService : IPasswordService
    {
        private readonly ILogger<PasswordService> _logger;
        private readonly UserManager<Customer> _userManager;
        private readonly IRefreshTokenService _refreshTokenService;
        private readonly IErrorNotificationService _errorNotificationService;
        private readonly IBackgroundJobClient _backgroundJobClient;
        private readonly IAccountEmailService _accountEmailService;

        public PasswordService(
            IAccountEmailService accountEmailService,
            IBackgroundJobClient backgroundJobClient,
            ILogger<PasswordService> logger,
            UserManager<Customer> userManager,
            IRefreshTokenService refreshTokenService,
            IErrorNotificationService errorNotificationService)
        {
            _accountEmailService = accountEmailService;
            _backgroundJobClient = backgroundJobClient;
            _logger = logger;
            _userManager = userManager;
            _refreshTokenService = refreshTokenService;
            _errorNotificationService = errorNotificationService;
        }

        public async Task<Result<bool>> ChangePasswordAsync(
            string userid,
            string oldPassword,
            string newPassword
        )
        {
            try
            {
                var user = await _userManager.FindByIdAsync(userid);
                if (user == null)
                {
                    _logger.LogWarning("Change password failed: User not found.");
                    return Result<bool>.Fail("User not found.", 401);
                }
                if (oldPassword.Equals(newPassword))
                {
                    _logger.LogWarning("Change password failed: New password same as old password");
                    return Result<bool>.Fail("Can't use the same password.", 400);
                }
                var result = await _userManager.ChangePasswordAsync(user, oldPassword, newPassword);
                if (!result.Succeeded)
                {
                    var errorMessages = string.Join("\nError: ", result.Errors.Select(e => e.Description));
                    _logger.LogError($"Failed to change password: {errorMessages}");
                    return Result<bool>.Fail($"Errors: {errorMessages}", 400);
                }
                _backgroundJobClient.Enqueue<PasswordService>(s => s.RemoveUserTokensAsync(userid));
                _backgroundJobClient.Enqueue(() => _accountEmailService.SendEmailAfterChangePassAsync(user.UserName,user.Email));
                _logger.LogInformation("Password changed successfully.");
                return Result<bool>.Ok(true, "Password changed successfully.", 200);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Exception in ChangePasswordAsync: {ex}");
                BackgroundJob.Enqueue<IErrorNotificationService>(e =>
                    e.SendErrorNotificationAsync(ex.Message, ex.StackTrace)
                );
                return Result<bool>.Fail("An unexpected error occurred.", 500);
            }
        }

        public async Task<Result<bool>> RequestPasswordResetAsync(string email)
        {
            try
            {
				var user = await _userManager.FindByEmailAsync(email);
                if (user != null)
                {

			    	var token = await _userManager.GeneratePasswordResetTokenAsync(user);
				    var encodedToken = System.Net.WebUtility.UrlEncode(token);
					_backgroundJobClient.Enqueue<IAccountEmailService>(e =>
                        e.SendPasswordResetEmailAsync(user.Email,user.UserName,encodedToken)
                    );
                }
                return Result<bool>.Ok(true, " a reset link has been sent.", 200);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in RequestPasswordResetAsync");
				_backgroundJobClient.Enqueue<IErrorNotificationService>(e =>
                    e.SendErrorNotificationAsync(ex.Message, ex.StackTrace)
                );
                return Result<bool>.Fail("An error occurred while requesting password reset.", 500);
            }
        }

        public async Task<Result<bool>> ResetPasswordAsync(
            string email,
            string token,
            string newPassword
        )
        {
            try
            {
                var user = await _userManager.FindByEmailAsync(email);
                if (user == null)
                {
                    _logger.LogWarning($"Password reset attempted for non-existent email: {email}");
					return Result<bool>.Ok(true, "Confirmation email has been resent. Please check your inbox.", 200);
				}
                var result = await _userManager.ResetPasswordAsync(user, token, newPassword);
                if (!result.Succeeded)
                {
                    var errors = string.Join(", ", result.Errors.Select(e => e.Description));
                    _logger.LogWarning($"Password reset failed for {email}: {errors}");
                    return Result<bool>.Fail($"Reset Failed: {errors}", 400);
                }
                BackgroundJob.Enqueue<IAccountEmailService>(e =>
                    e.SendPasswordResetSuccessEmailAsync(email)
                );
                BackgroundJob.Enqueue<PasswordService>(s => s.RemoveUserTokensAsync(user.Id));
                return Result<bool>.Ok(true, "Password has been reset successfully.", 200);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in ResetPasswordAsync");
                BackgroundJob.Enqueue<IErrorNotificationService>(e =>
                    e.SendErrorNotificationAsync(ex.Message, ex.StackTrace)
                );
                return Result<bool>.Fail("An error occurred while resetting password.", 500);
            }
        }

        public async Task RemoveUserTokensAsync(string userid)
        {
            try
            {
                await _refreshTokenService.RemoveRefreshTokenAsync(userid);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error in RemoveUserTokensAsync: {ex.Message}");
            }
        }
    }
} 