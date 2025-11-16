using DomainLayer.Enums;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DomainLayer.Models
{
	public class PaymentMethod : BaseEntity
	{
		

		[Required(ErrorMessage = "Payment method name is required.")]
		[StringLength(20, MinimumLength = 3)]
		public string Name { get; set; } = string.Empty;

		public PaymentMethodEnums Method { get; set; }

		[ForeignKey("PaymentProviders")]
		public int PaymentProviderId { get; set; }
		public bool IsActive { get; set; }
		public PaymentProvider PaymentProviders { get; set; }
		public string? IntegrationId { get; set; }
		public ICollection<Payment> Payments { get; set; } = new List<Payment>();
	}

}
