using System.ComponentModel.DataAnnotations;

namespace ApplicationLayer.DtoModels.WareHouseDtos
{
	public class CreateWareHouseDto
	{
		[Required(ErrorMessage = "Name Required")]
		[StringLength(50, MinimumLength = 3, ErrorMessage = "Must Between 3 to 50 characters")]
		[RegularExpression(@"^[a-zA-Z0-9][a-zA-Z0-9\s\-,]*[a-zA-Z0-9]$", ErrorMessage = "Name must start and end with an alphanumeric character and can contain spaces, hyphens, and commas in between.")]
		public string Name { get; set; } = string.Empty;
		
		[Required(ErrorMessage = "Address Required")]
		[StringLength(200, MinimumLength = 10, ErrorMessage = "Must Between 10 to 200 characters")]
		public string Address { get; set; } = string.Empty;
		
		[Phone]
		[StringLength(20, ErrorMessage = "Phone number too long")]
		public string Phone { get; set; } = string.Empty;
		
		[StringLength(100, ErrorMessage = "Email too long")]
		[EmailAddress(ErrorMessage = "Invalid email format")]
		public string? Email { get; set; }
		
		[StringLength(500, ErrorMessage = "Description too long")]
		public string? Description { get; set; }
		
		public string? ManagerName { get; set; }
		
		[Phone]
		[StringLength(20, ErrorMessage = "Manager phone number too long")]
		public string? ManagerPhone { get; set; }
	}
}


