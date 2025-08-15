using System.ComponentModel.DataAnnotations.Schema;

namespace E_Commerce.Models
{
	public class Item
	{
		public int Id { get; set; }

		[ForeignKey("Order")]
		public int OrderId { get; set; }
		public  Order Order { get; set; }

		[ForeignKey("Product")]
		public int ProductId { get; set; }
		public  Product Product { get; set; }

		public  int Quantity { get; set; }
		public DateTime AddedAt { get; set; } = DateTime.UtcNow;
	}
}
