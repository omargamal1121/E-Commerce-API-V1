using Domain.Models;
using Application.Services;

namespace Application.Interfaces
{
	public interface ITokenService 
	{
		public Task<Result<string>> GenerateTokenAsync(Customer user);
		public  Task<Result<string>> GenerateTokenAsync(string userId);


	}
}


