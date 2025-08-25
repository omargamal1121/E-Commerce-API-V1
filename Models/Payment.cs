using E_Commerce.Enums;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace E_Commerce.Models
{
	public class Payment:BaseEntity
	{
	

		[ForeignKey("Customer")]
		public string CustomerId { get; set; } = string.Empty;
		public  Customer Customer { get; set; }

		[ForeignKey("PaymentMethod")]
		public int PaymentMethodId { get; set; }
		public  PaymentMethod PaymentMethod { get; set; }

		[ForeignKey("PaymentProvider")]
		public int PaymentProviderId { get; set; }
		public PaymentProvider PaymentProvider { get; set; }

		[Required]
		[Range(0.01, double.MaxValue, ErrorMessage = "Amount must be greater than zero.")]
		public decimal Amount { get; set; }

		public DateTime PaymentDate { get; set; } = DateTime.UtcNow;

		[ForeignKey("Order")]
		public int OrderId { get; set; }
		public  Order Order { get; set; }

		[Required(ErrorMessage = "Payment Status is required.")]
		[StringLength(20, MinimumLength = 3, ErrorMessage = "Status must be between 3 and 20 characters.")]
		public PaymentStatus Status { get; set; }
		[StringLength(100)]
		public string? TransactionId { get; set; }

		public long ProviderOrderId { get; set; }

		public ICollection<PaymentWebhook> Webhooks { get; set; } = new List<PaymentWebhook>();
	}
}
