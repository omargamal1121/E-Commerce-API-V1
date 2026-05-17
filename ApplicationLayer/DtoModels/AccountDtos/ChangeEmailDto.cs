using System.ComponentModel.DataAnnotations;

namespace Application.DtoModels.AccountDtos
{
	public class ChangeEmailDto
	{
		[EmailAddress]
		public string Email { get; set; }
	}
}


