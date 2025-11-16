using System.ComponentModel.DataAnnotations;

namespace ApplicationLayer.DtoModels.AccountDtos
{
	public class ChangeEmailDto
	{
		[EmailAddress]
		public string Email { get; set; }
	}
}


