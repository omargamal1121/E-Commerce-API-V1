using DomainLayer.Models;

namespace ApplicationLayer.Services.EmailServices
{
    public interface IAccountEmailService
    {
        public  Task SendValidationEmailAsync(string email, string userId, string token, string frontendUrl);

		public Task SendPasswordResetEmailAsync(string email, string username, string token);
        public Task SendPasswordResetSuccessEmailAsync(string email);
        public Task SendAccountLockedEmailAsync(string email, string username, string reason = "Multiple failed login attempts");

          Task SendWelcomeEmailAsync(string email, string username);

		 Task SendEmailAfterChangePassAsync(string username, string email);

	}
} 

