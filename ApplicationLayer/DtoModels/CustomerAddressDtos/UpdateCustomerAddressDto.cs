using System.ComponentModel.DataAnnotations;

namespace ApplicationLayer.DtoModels.CustomerAddressDtos
{
	public class UpdateCustomerAddressDto
	{
		[StringLength(50, MinimumLength = 2, ErrorMessage = "First Name must be between 2 and 50 characters")]
		[RegularExpression(@"^[a-zA-Z\s]+$", ErrorMessage = "First Name can only contain letters and spaces")]
		public string? FirstName { get; set; }

		[StringLength(50, MinimumLength = 2, ErrorMessage = "Last Name must be between 2 and 50 characters")]
		[RegularExpression(@"^[a-zA-Z\s]+$", ErrorMessage = "Last Name can only contain letters and spaces")]
		public string? LastName { get; set; }

		[Phone(ErrorMessage = "Invalid phone number format")]
		[StringLength(20, ErrorMessage = "Phone number too long")]
		public string? PhoneNumber { get; set; }

		[StringLength(50, MinimumLength = 2, ErrorMessage = "Country must be between 2 and 50 characters")]
		public string? Country { get; set; }

		[StringLength(50, MinimumLength = 2, ErrorMessage = "State/Province must be between 2 and 50 characters")]
		public string? State { get; set; }

		[StringLength(50, MinimumLength = 2, ErrorMessage = "City must be between 2 and 50 characters")]
		public string? City { get; set; }

		[StringLength(200, MinimumLength = 5, ErrorMessage = "Street Address must be between 5 and 200 characters")]
		public string? StreetAddress { get; set; }

		[StringLength(100, ErrorMessage = "Apartment/Suite too long")]
		public string? ApartmentSuite { get; set; }

		[StringLength(20, MinimumLength = 3, ErrorMessage = "Postal Code must be between 3 and 20 characters")]
		[RegularExpression(@"^[a-zA-Z0-9\s\-]+$", ErrorMessage = "Postal Code can only contain letters, numbers, spaces, and hyphens")]
		public string? PostalCode { get; set; }

		[StringLength(20, ErrorMessage = "Address Type too long")]
		public string? AddressType { get; set; }

		public bool? IsDefault { get; set; }

		[StringLength(500, ErrorMessage = "Additional Notes too long")]
		public string? AdditionalNotes { get; set; }
	}
} 

