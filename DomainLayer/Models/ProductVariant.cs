using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using DomainLayer.Enums;

namespace DomainLayer.Models
{
    public class ProductVariant : BaseEntity
    {
        [Required]
        public string Color { get; set; }

        [Timestamp]
        [Column(TypeName = "binary(8)")]
        public byte[] RowVersion { get; set; }
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