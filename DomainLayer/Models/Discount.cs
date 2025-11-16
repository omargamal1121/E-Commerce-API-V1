using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace DomainLayer.Models
{
	public class Discount:BaseEntity
	{
		[Required(ErrorMessage = " Name is required.")]
		[StringLength(
	20,
	MinimumLength = 5,
	ErrorMessage = " Name must be between 5 and 20 characters."
)]
		[RegularExpression(@"^[a-zA-Z0-9][a-zA-Z0-9\s\-,]*[a-zA-Z0-9]$", ErrorMessage = "Name must start and end with an alphanumeric character and can contain spaces, hyphens, and commas in between.")]
		public string Name { get; set; } = string.Empty;

		[Required(ErrorMessage = " Description is required.")]
		[StringLength(
			50,
			MinimumLength = 10,
			ErrorMessage = " Description must be between 10 and 50 characters."
		)]
		[RegularExpression(@"^[\w\s.,\-()'\""]{0,500}$", ErrorMessage = "Description can contain up to 500 characters: letters, numbers, spaces, and .,-()'\"")]
		public string Description { get; set; } = string.Empty;


		[Required(ErrorMessage = "Discount Percent Required")]
		[Range(1, 100, ErrorMessage = "Discount percentage must be between 1 and 100")]
		public decimal DiscountPercent { get; set; }

		[Required(ErrorMessage = "Start date is required")]
		public DateTime StartDate { get; set; }

		[Required(ErrorMessage = "End date is required")]
		public DateTime EndDate { get; set; }

		public bool IsActive { get; set; } = false;

		[JsonIgnore]
		public List<Product> products { get; set; } = new List<Product>();

		public int? CategoryId { get; set; }

		[JsonIgnore]
		public Category? Category { get; set; }

	}
}
