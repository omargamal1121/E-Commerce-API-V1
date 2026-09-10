using System.ComponentModel.DataAnnotations;

namespace Domain.Models
{
	public class SubCategory:BaseEntity
	{
		
		[RegularExpression(@"^[a-zA-Z0-9][a-zA-Z0-9\s\-,]*[a-zA-Z0-9]$", ErrorMessage = "Name must start and end with an alphanumeric character and can contain spaces, hyphens, and commas in between.")]
		public string Name { get; set; } = string.Empty;

		[Required(ErrorMessage = "Description is required.")]
		[RegularExpression(@"^[\w\s.,\-()'\""]*$", ErrorMessage = "Description can contain letters, numbers, spaces, and .,-()'\"")]
		public string Description { get; set; } = string.Empty;

		public int CategoryId { get; set; }
		public Category Category { get; set; }
		public ICollection<Image> Images { get; set; } = new List<Image>();
		public ICollection<Product> Products { get; set; } = new List<Product>();
		public bool IsActive { get; set; } = false;
	}
}
