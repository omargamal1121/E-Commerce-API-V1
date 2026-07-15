using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Domain.Enums;

namespace Domain.Models
{
	public class Order : BaseEntity
	{
		[ForeignKey("Customer")]
		public string ?CustomerId { get; set; } = string.Empty;
		public  Customer ?Customer { get; set; }
		[ForeignKey("CustomerAddress")]
		public int? CustomerAddressId { get; set; }

		public CustomerAddress?  CustomerAddress { get; set; }

		[Required]
		[StringLength(50, MinimumLength = 3, ErrorMessage = "Order number must be between 3 and 50 characters")]
		public string OrderNumber { get; set; } = string.Empty;

		[Required]
		public OrderStatus Status { get; set; } = OrderStatus.PendingPayment;

		[Range(0.01, double.MaxValue, ErrorMessage = "Subtotal must be greater than zero")]
		public decimal Subtotal { get; set; }

		[Range(0, double.MaxValue, ErrorMessage = "Tax amount cannot be negative")]
		public decimal TaxAmount { get; set; }

		[Range(0, double.MaxValue, ErrorMessage = "Shipping cost cannot be negative")]
		public decimal ShippingCost { get; set; }

		[Range(0, double.MaxValue, ErrorMessage = "Discount amount cannot be negative")]
		public decimal DiscountAmount { get; set; }

		[Range(0.01, double.MaxValue, ErrorMessage = "Total must be greater than zero")]
		[Required(ErrorMessage = "Total Required")]
		public decimal Total { get; set; }

		[StringLength(500, ErrorMessage = "Notes cannot exceed 500 characters")]
		public string? Notes { get; set; }

		public DateTime? ShippedAt { get; set; }
		public DateTime? DeliveredAt { get; set; }
		public DateTime? CancelledAt { get; set; }
		public DateTime? RestockedAt { get; set; }

        [Timestamp]
        [Column(TypeName = "binary(8)")]
        public byte[]? RowVersion { get; set; } = null;
        public ICollection<OrderItem> Items { get; set; } = new List<OrderItem>();
		public ICollection<Payment>? Payment { get; set; }
		public ICollection<ReturnRequest> ReturnRequests { get; set; } = new List<ReturnRequest>();
		public string? CustomerName { get; set; }
		public string? PhoneNumber { get; set; }
		public string? Email { get; set; }

		public string? Governorate { get; set; }
        public string? City { get; set; }
        public string? Street { get; set; }
        public string? Building { get; set; }
        public bool IsGuest { get; set; }
        public string? GuestTokenHash { get; set; }
		
	

	}
}
