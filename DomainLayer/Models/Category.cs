using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using Domain.Enums;

namespace Domain.Models
{
    public class Category : BaseEntity
    {
        [Required(ErrorMessage = "Name is required.")]
       public string Name { get; set; } = string.Empty;

        [Required(ErrorMessage = "Description is required.")]
		[RegularExpression(@"^[\w\s.,\-()'\""]*$", ErrorMessage = "Description can contain letters, numbers, spaces, and .,-()'\"")]
		public string Description { get; set; } = string.Empty;

		[Range(0, 5, ErrorMessage = "Display order must be between 0 (highest) and 5 (lowest)")]
		public int DisplayOrder { get; set; }
        public bool IsActive { get; set; } = false;

		public ICollection<SubCategory> SubCategories { get; set; }

		public ICollection<Image> Images { get; set; } = new List<Image>();
    }
}
