using System.ComponentModel.DataAnnotations;

namespace E_Commerce.DtoModels.CustomerAddressDtos
{
	public class CreateCustomerAddressDto
	{
		[Required(ErrorMessage = "Phone Number Required")]
		[Phone(ErrorMessage = "Invalid phone number format")]
		[StringLength(20, ErrorMessage = "Phone number too long")]
		public string PhoneNumber { get; set; } = string.Empty;

		[Required(ErrorMessage = "Country Required")]
		[StringLength(50, MinimumLength = 2, ErrorMessage = "Country must be between 2 and 50 characters")]
		public string Country { get; set; } = string.Empty;

		[Required(ErrorMessage = "State/Province Required")]
		[StringLength(50, MinimumLength = 2, ErrorMessage = "State/Province must be between 2 and 50 characters")]
		public string State { get; set; } = string.Empty;

		[Required(ErrorMessage = "City Required")]
		[StringLength(50, MinimumLength = 2, ErrorMessage = "City must be between 2 and 50 characters")]
		public string City { get; set; } = string.Empty;

		[Required(ErrorMessage = "Street Address Required")]
		[StringLength(200, MinimumLength = 5, ErrorMessage = "Street Address must be between 5 and 200 characters")]
		public string StreetAddress { get; set; } = string.Empty;


		[Required(ErrorMessage = "Postal Code Required")]
		[StringLength(20, MinimumLength = 3, ErrorMessage = "Postal Code must be between 3 and 20 characters")]
		[RegularExpression(@"^[a-zA-Z0-9\s\-]+$", ErrorMessage = "Postal Code can only contain letters, numbers, spaces, and hyphens")]
		public string PostalCode { get; set; } = string.Empty;

		public bool IsDefault { get; set; } = false;

		[StringLength(500, ErrorMessage = "Additional Notes too long")]
		public string? AdditionalNotes { get; set; }
	}
} 