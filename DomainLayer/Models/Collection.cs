using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace DomainLayer.Models
{
    public class Collection : BaseEntity
    {
        [Required]
        [RegularExpression(@"^[a-zA-Z0-9][a-zA-Z0-9\s\-,]*[a-zA-Z0-9]$", ErrorMessage = "Name must start and end with an alphanumeric character and can contain spaces, hyphens, and commas in between.")]
        public string Name { get; set; } = string.Empty;
        
		[RegularExpression(@"^[\w\s.,\-()'\""]{0,500}$", ErrorMessage = "Description can contain up to 500 characters: letters, numbers, spaces, and .,-()'\"")]
		public string? Description { get; set; }
		
		[Range(0, 5, ErrorMessage = "Display order must be between 0 (highest) and 5 (lowest)")]
		public int DisplayOrder { get; set; }
		
		public bool IsActive { get; set; } = false;
		
		public ICollection<ProductCollection> ProductCollections { get; set; } = new List<ProductCollection>();
        public ICollection<Image> Images { get; set; } = new List<Image>();
    }
} 