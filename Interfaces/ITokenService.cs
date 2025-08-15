using E_Commerce.Models;
using E_Commerce.Services;

namespace E_Commerce.Interfaces
{
	public interface ITokenService 
	{
		public Task<Result<string>> GenerateTokenAsync(Customer user);
		public  Task<Result<string>> GenerateTokenAsync(string userId);


	}
}
