using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using E_Commerce.Enums;

namespace E_Commerce.Models
{
    public class ProductVariant : BaseEntity
    {
        [Required]
        public string Color { get; set; }
        
        public VariantSize? Size { get; set; }
        public int? Waist { get; set; }
        public int? Length { get; set; }

        [Required]
        public int Quantity { get; set; }

        [Required]
        public int ProductId { get; set; }
        public Product Product { get; set; }
		public  bool IsActive { get; set; }
		public ICollection<CartItem> CartItems { get; set; } = new List<CartItem>();
		public ICollection<OrderItem> OrderItems { get; set; } = new List<OrderItem>();
	}
} 