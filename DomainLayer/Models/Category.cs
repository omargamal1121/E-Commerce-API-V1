using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using DomainLayer.Enums;

namespace DomainLayer.Models
{
    public class Category : BaseEntity
    {
        [Required(ErrorMessage = "Name is required.")]
        [StringLength(
            20,
            MinimumLength = 5,
            ErrorMessage = "Name must be between 5 and 20 characters."
        )]
        [RegularExpression(@"^[a-zA-Z0-9][a-zA-Z0-9\s\-,]*[a-zA-Z0-9]$", ErrorMessage = "Name must start and end with an alphanumeric character and can contain spaces, hyphens, and commas in between.")]
        public string Name { get; set; } = string.Empty;

        [Required(ErrorMessage = "Description is required.")]
        [StringLength(
            50,
            MinimumLength = 10,
            ErrorMessage = "Description must be between 10 and 50 characters."
        )]
		[RegularExpression(@"^[\w\s.,\-()'\""]{0,500}$", ErrorMessage = "Description can contain up to 500 characters: letters, numbers, spaces, and .,-()'\"")]

		public string Description { get; set; } = string.Empty;

		[Range(0, 5, ErrorMessage = "Display order must be between 0 (highest) and 5 (lowest)")]
		public int DisplayOrder { get; set; }
        public bool IsActive { get; set; } = false;

		public ICollection<SubCategory> SubCategories { get; set; }

		public ICollection<Image> Images { get; set; } = new List<Image>();
    }
}
