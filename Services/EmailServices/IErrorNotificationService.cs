namespace E_Commerce.Services.EmailServices
{
	public interface IErrorNotificationService
    {
       public  Task SendErrorNotificationAsync(string errorMessage, string? stackTrace = null);
	}
} 