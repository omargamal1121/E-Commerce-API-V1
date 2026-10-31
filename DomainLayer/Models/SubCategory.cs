using System.ComponentModel.DataAnnotations;

namespace Domain.Models
{
	public class SubCategory:BaseEntity
	{
		[Required(ErrorMessage ="Name is required")]
		
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
