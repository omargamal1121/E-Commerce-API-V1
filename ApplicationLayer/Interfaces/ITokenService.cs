using DomainLayer.Models;
using ApplicationLayer.Services;

namespace ApplicationLayer.Interfaces
{
	public interface ITokenService 
	{
		public Task<Result<string>> GenerateTokenAsync(Customer user);
		public  Task<Result<string>> GenerateTokenAsync(string userId);


	}
}


