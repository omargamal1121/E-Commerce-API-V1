using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Domain.Models
{
    public class Collection : BaseEntity
    {
        [Required]
     public string Name { get; set; } = string.Empty;
        
		[RegularExpression(@"^[\w\s.,\-()'\""]*$", ErrorMessage = "Description can contain letters, numbers, spaces, and .,-()'\"")]
		public string? Description { get; set; }
		
		[Range(0, 5, ErrorMessage = "Display order must be between 0 (highest) and 5 (lowest)")]
		public int DisplayOrder { get; set; }
		
		public bool IsActive { get; set; } = false;
		
		public ICollection<ProductCollection> ProductCollections { get; set; } = new List<ProductCollection>();
        public ICollection<Image> Images { get; set; } = new List<Image>();
    }
} 