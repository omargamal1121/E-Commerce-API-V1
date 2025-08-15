using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace E_Commerce.Models
{
	public class Cart : BaseEntity
	{
		[Required]
		public string UserId { get; set; } = string.Empty;

		[ForeignKey("Customer")]
		public string CustomerId { get; set; } = string.Empty;
		public Customer Customer { get; set; }

		public List<CartItem> Items { get; set; } = new List<CartItem>();

		public bool IsEmpty => !Items.Any();
		public int TotalItems => Items.Sum(item => item.Quantity);
		public decimal TotalPrice => Items.Sum(item => item.UnitPrice * item.Quantity);
		public DateTime? CheckoutDate { get; set; }


	}
}
