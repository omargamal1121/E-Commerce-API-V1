using DomainLayer.Models;
using Hangfire;
using InfrastructureLayer.Context;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace InfrastructureLayer.Repository
{
	public class UsersRepository 
	{
		private readonly AppDbContext _context;
		private readonly ILogger<UsersRepository> _logger;
		private readonly UserManager<Customer> _userManager;
		private readonly BackgroundJobClient _backgroundJobClient;
        public UsersRepository(BackgroundJobClient backgroundJobClient ,AppDbContext context, ILogger<UsersRepository> logger, UserManager<Customer> userManager) 
		{
			_backgroundJobClient = backgroundJobClient;
            _context = context;
			_logger = logger;
			_userManager = userManager;
        }


		public async Task<List<IdentityUserRole<string>>> GetUserRolesAsync(string userid)
		{
			_logger.LogInformation("Getting roles for user {UserId}...", userid);
			var userRoles = await _context.UserRoles
				.Where(ur => ur.UserId == userid)
				.ToListAsync();
			return userRoles;
        }
		public async Task<string?> GeneratePasswordResetTokenAsync(Customer user)
		{
			_logger.LogInformation("Generating password reset token for user {email}...", user.Email);
			var token = await _userManager.GeneratePasswordResetTokenAsync(user);
			return token;
        }


		public async Task<IdentityResult> ResetPasswordAsync(Customer user, string token, string newPassword)
		{
			_logger.LogInformation("Resetting password for user {email}...", user.Email);
			var result = await _userManager.ResetPasswordAsync(user, token, newPassword);
			return result;
        }
		public async Task<IdentityResult> SetEmailAsync(Customer user, string newEmail)
		{
			_logger.LogInformation("Setting new email for user {oldEmail} to {newEmail}...", user.Email, newEmail);
			var result = await _userManager.SetEmailAsync(user, newEmail);
			return result;
        }

		public async Task<string?> GenerateEmailConfirmationTokenAsync(Customer user)
		{
			_logger.LogInformation("Generating email confirmation token for user {email}...", user.Email);
			var token = await _userManager.GenerateEmailConfirmationTokenAsync(user);
			return token;
        }
		public async Task<IdentityResult> ConfirmEmailAsync(Customer user, string token)
		{
			_logger.LogInformation("Confirming email for user {email}...", user.Email);
			var result = await _userManager.ConfirmEmailAsync(user, token);
			return result;
        }
        public async Task<IdentityResult> ChangePasswordAsync(Customer user, string oldPassword, string newPassword)
		{
			_logger.LogInformation("Changing password for user {email}...", user.Email);
			var result = await _userManager.ChangePasswordAsync(user, oldPassword, newPassword);
			return result;
        }

        public async Task<IdentityResult> CreateUserAsync(Customer user, string password)
		{
			_logger.LogInformation("Creating user {email}...", user.Email);
            var result = await  _userManager.CreateAsync(user, password);
			return result;
		}

        public async Task<IdentityResult> DeletedUserAsync(string userid)
        {
            _logger.LogInformation("Deleting user {UserId}...", userid);

            var user = await _userManager.FindByIdAsync(userid);

            if (user == null || user.DeletedAt != null)
            {
                _logger.LogWarning("User {UserId} not found or already deleted.", userid);
                return IdentityResult.Failed(new IdentityError { Description = $"User {userid} not found or already deleted." });
            }

            user.DeletedAt = DateTime.UtcNow;

            var result = await _userManager.UpdateAsync(user);
			_backgroundJobClient.Enqueue(()=>	 _userManager.UpdateSecurityStampAsync(user));


			return result;
        }

        public async Task<IdentityResult> CheckPassword(Customer user, string password)
		{
			_logger.LogInformation("Checking password for user {email}...", user.Email);
			var result = await _userManager.CheckPasswordAsync(user, password);
			if (result)
			{
				return IdentityResult.Success;
			}
			else
			{
				return IdentityResult.Failed(new IdentityError { Description = "Invalid Email Or password." });
            }
        }
		public async Task<Customer?> FindByEmailAsync(string email)
		{
			_logger.LogInformation("Finding user by email {email}...", email);
			var user = await _userManager.FindByEmailAsync(email);
			if (user == null) 
			{
				_logger.LogWarning("User with email {email} not found.", email);
				return null;
            }
			return user;
        }
		public async Task<Customer?> FindByIdAsync(string id)
		{
			_logger.LogInformation("Finding user by id {id}...", id);
			var user = await _userManager.FindByIdAsync(id);
			if (user == null)
			{
				_logger.LogWarning("User with id {id} not found.", id);
				return null;
            }
			return user;
        }
		public async Task<bool> IsLockedOutAsync(Customer user)
		{
			_logger.LogInformation("Checking if user {email} is locked out...", user.Email);
			var result = await _userManager.IsLockedOutAsync(user);
			return result;
        }
		public async Task<IdentityResult> ResetAccessFailedCountAsync(Customer user)
		{
			_logger.LogInformation("Resetting access failed count for user {email}...", user.Email);
			var result = await _userManager.ResetAccessFailedCountAsync(user);
			return result;
        }
		public async Task<IdentityResult> UpdateSecurityStampAsync(Customer user)
		{
			_logger.LogInformation("Updating security stamp for user {email}...", user.Email);
			var result = await _userManager.UpdateSecurityStampAsync(user);
			return result;
        }

		public async Task<IdentityResult> AccessFailedAsync(Customer user)
		{
			_logger.LogInformation("Incrementing access failed count for user {email}...", user.Email);
			var result = await _userManager.AccessFailedAsync(user);
			return result;
        }

    }
}
