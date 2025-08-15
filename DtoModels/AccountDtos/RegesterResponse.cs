using System.ComponentModel.DataAnnotations;

namespace E_Commerce.DtoModels.AccountDtos
{
	public class RegisterResponse
	{
		[RegularExpression(@"^[a-zA-Z0-9][a-zA-Z0-9\s\-,]*[a-zA-Z0-9]$", ErrorMessage = "Name must start and end with an alphanumeric character and can contain spaces, hyphens, and commas in between.")]
		public string Name { get; set; } = string.Empty;
		public string UserName { get; set; } = string.Empty;
		[Required]
		public string PhoneNumber { get; set; } = string.Empty;
		public int Age { get; set; }
		public string Email { get; set; } = string.Empty;


	}
}
