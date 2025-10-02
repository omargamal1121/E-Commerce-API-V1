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

        public async Task<Result<bool>> ChangePasswordAsync(string userid, string oldPassword, string newPassword)
        {
            try
            {
                var user = await _userManager.FindByIdAsync(userid);
                if (user == null || user.DeletedAt != null)
                {
                    _logger.LogWarning("Change password failed: User {UserId} not found or deleted.", userid);
                    return Result<bool>.Fail("User not found.", 404);
                }

                if (oldPassword.Equals(newPassword))
                {
                    _logger.LogWarning("Change password failed: User {UserId} tried same old password.", userid);
                    return Result<bool>.Fail("Can't use the same password.", 400);
                }

                var result = await _userManager.ChangePasswordAsync(user, oldPassword, newPassword);
                if (!result.Succeeded)
                {
                    var errors = string.Join(", ", result.Errors.Select(e => e.Description));
                    _logger.LogError("Change password failed for {UserId}. Errors: {Errors}", userid, errors);
                    return Result<bool>.Fail($"Errors: {errors}", 400);
                }

                _backgroundJobClient.Enqueue(() => _refreshTokenService.RemoveRefreshTokenAsync(userid));
                _backgroundJobClient.Enqueue(() => _accountEmailService.SendEmailAfterChangePassAsync(user.UserName, user.Email));

                _logger.LogInformation("Password changed successfully for user {UserId}", userid);
                return Result<bool>.Ok(true, "Password changed successfully.", 200);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception in ChangePasswordAsync for user {UserId}", userid);
                _backgroundJobClient.Enqueue(() => _errorNotificationService.SendErrorNotificationAsync(ex.Message, ex.StackTrace));
                return Result<bool>.Fail("An unexpected error occurred.", 500);
            }
        }

        public async Task<Result<bool>> RequestPasswordResetAsync(string email)
        {
            try
            {
                var user = await _userManager.FindByEmailAsync(email);

                if (user != null && user.DeletedAt == null)
                {
                    var token = await _userManager.GeneratePasswordResetTokenAsync(user);
                    var encodedToken = System.Net.WebUtility.UrlEncode(token);

                    _backgroundJobClient.Enqueue(() =>
                        _accountEmailService.SendPasswordResetEmailAsync(user.Email, user.UserName, encodedToken));
                }

                // Always return success (don't reveal user existence)
                return Result<bool>.Ok(true, "If the email exists, a reset link has been sent.", 200);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in RequestPasswordResetAsync for email {Email}", email);
                _backgroundJobClient.Enqueue(() => _errorNotificationService.SendErrorNotificationAsync(ex.Message, ex.StackTrace));
                return Result<bool>.Fail("An error occurred while requesting password reset.", 500);
            }
        }

        public async Task<Result<bool>> ResetPasswordAsync(string email, string token, string newPassword)
        {
            try
            {
                var user = await _userManager.FindByEmailAsync(email);

                if (user == null || user.DeletedAt != null)
                {
                    _logger.LogWarning("Password reset requested for non-existent or deleted user {Email}", email);
                    // Same behavior: don't reveal user existence
                    return Result<bool>.Ok(true, "If your account exists, you will receive a confirmation email.", 200);
                }

                var result = await _userManager.ResetPasswordAsync(user, token, newPassword);
                if (!result.Succeeded)
                {
                    var errors = string.Join(", ", result.Errors.Select(e => e.Description));
                    _logger.LogWarning("Password reset failed for {Email}. Errors: {Errors}", email, errors);
                    return Result<bool>.Fail($"Reset Failed: {errors}", 400);
                }

                _backgroundJobClient.Enqueue(() => _accountEmailService.SendPasswordResetSuccessEmailAsync(email));
                _backgroundJobClient.Enqueue(() => _refreshTokenService.RemoveRefreshTokenAsync(user.Id));

                _logger.LogInformation("Password reset successful for user {Email}", email);
                return Result<bool>.Ok(true, "Password has been reset successfully.", 200);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in ResetPasswordAsync for {Email}", email);
                _backgroundJobClient.Enqueue(() => _errorNotificationService.SendErrorNotificationAsync(ex.Message, ex.StackTrace));
                return Result<bool>.Fail("An error occurred while resetting password.", 500);
            }
        }
    }
}
