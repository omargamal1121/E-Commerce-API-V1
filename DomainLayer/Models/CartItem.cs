using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DomainLayer.Models
{
    public class CartItem : BaseEntity
    {
        [ForeignKey("Cart")]
        public int CartId { get; set; }
        public Cart Cart { get; set; }

        [ForeignKey("Product")]
        public int ProductId { get; set; }
        public Product Product { get; set; }

        [ForeignKey("ProductVariant")]
        public int? ProductVariantId { get; set; }
        public ProductVariant? ProductVariant { get; set; }

        [Required]
        [Range(1, 100, ErrorMessage = "Quantity must be between 1 and 100")]
        public int Quantity { get; set; }

		public decimal UnitPrice
		{ get; set; }

        [Timestamp]
        [Column(TypeName = "binary(8)")]
        public byte[]? RowVersion { get; set; } = null;

        public DateTime AddedAt { get; set; } = DateTime.UtcNow;
    }
} 