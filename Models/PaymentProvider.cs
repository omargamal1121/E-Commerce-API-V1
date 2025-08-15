using E_Commerce.Enums;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace E_Commerce.Models
{
	public class PaymentProvider:BaseEntity
	{
	
		[Required(ErrorMessage = "Payment Provider name is required.")]
		[StringLength(50, MinimumLength = 3, ErrorMessage = "Name must be between 3 and 50 characters.")]
		[RegularExpression(@"^[a-zA-Z0-9][a-zA-Z0-9\s\-,]*[a-zA-Z0-9]$", ErrorMessage = "Name must start and end with an alphanumeric character and can contain spaces, hyphens, and commas in between.")]
		public string Name { get; set; } = string.Empty;

		[Required(ErrorMessage = "API Endpoint is required.")]
		[StringLength(100, MinimumLength = 3, ErrorMessage = "API must be between 3 and 100 characters.")]
		public string ApiEndpoint { get; set; } = string.Empty;

		[StringLength(500, ErrorMessage = "Public Key is too long.")]
		public string? PublicKey { get; set; }

		[StringLength(200, ErrorMessage = "Private Key is too long.")]
		public string? PrivateKey { get; set; }



		public ICollection<PaymentMethod> PaymentMethods { get; set; } = new List<PaymentMethod>();
		public PaymentProviderEnums Provider { get; set; }

		public string? Hmac { get; set; }

		public string? IframeId { get; set; }


		public ICollection<Payment> Payments { get; set; } = new List<Payment>();
	}
}
