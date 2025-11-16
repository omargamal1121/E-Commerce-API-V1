using DomainLayer.Enums;
using System.ComponentModel.DataAnnotations;
namespace ApplicationLayer.DtoModels.AccountDtos
{
	public class RegisterDto
	{
		[Required(ErrorMessage = "Name is required.")]
		[RegularExpression(@"^[a-zA-Z](?:[a-zA-Z\s\-,]*[a-zA-Z])?$", ErrorMessage = "Name must start and end with a letter and may contain spaces, hyphens, and commas in between.")]
		[Display(Name = "Full Name")]
		public string Name { get; set; } = string.Empty;

		[Required(ErrorMessage = "Name is required.")]
		[RegularExpression(@"^(?![_\.])[a-zA-Z0-9._]+(?<![_\.])$", ErrorMessage = "User name must contain only letters, numbers, dots, and underscores, and must not start or end with a dot or underscore.")]
		[Display(Name = "User Name")]
		public string UserName { get; set; } = string.Empty;

		[Required(ErrorMessage = "Phone number is required.")]
		[Phone(ErrorMessage = "Invalid phone number format.")]
		[RegularExpression(@"^01[0-9]{9}$", ErrorMessage = "Phone number must be a valid 11-digit Egyptian number starting with 01.")]
		[Display(Name = "Phone Number")]
		public string PhoneNumber { get; set; } = string.Empty;

		[Required(ErrorMessage = "Age is required.")]
		[Range(18, 100, ErrorMessage = "Age must be between 18 and 100.")]
		[Display(Name = "Age")]
		public int Age { get; set; }

		[Required(ErrorMessage = "Email address is required.")]
		[EmailAddress(ErrorMessage = "Invalid email format.")]
		[Display(Name = "Email Address")]
		public string Email { get; set; } = string.Empty;
		public Gender Gender { get; set; }


		[Required(ErrorMessage = "Password is required.")]
		[Display(Name = "Password")]
		public string Password { get; set; } = string.Empty;

		[Required(ErrorMessage = "Confirm password is required.")]
		[Compare("Password", ErrorMessage = "Confirm password does not match the password.")]
		[Display(Name = "Confirm Password")]
		public string ConfirmPassword { get; set; } = string.Empty;
	}}


