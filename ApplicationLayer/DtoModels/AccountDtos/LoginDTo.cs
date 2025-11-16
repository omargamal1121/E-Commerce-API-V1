using System.ComponentModel.DataAnnotations;

namespace ApplicationLayer.DtoModels.AccountDtos
{
	public class LoginDTo
	{
		[Required(ErrorMessage = "Email Address Required")]
		[EmailAddress(ErrorMessage = "Invalid Email")]
		[Display(Name ="Email Address")]
		public string Email { get; set; } = string.Empty;
		[Required(ErrorMessage = "Password  Required")]
		
		[Display(Name = "Password")]
		public string Password { get; set; } = string.Empty;
	}
}


