using System.ComponentModel.DataAnnotations;

namespace E_Commerce.DtoModels.AccountDtos
{
	public class ChangeEmailDto
	{
		[EmailAddress]
		public string Email { get; set; }
	}
}
