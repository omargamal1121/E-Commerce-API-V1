
using Domain.Enums;
using System.ComponentModel.DataAnnotations;

namespace Application.DtoModels.PaymentDtos
{
    public class CreatePayment
    {
        public string CustomerId { get; set; } = string.Empty;
		public string? CustomerName { get; set; }
		public string? PhoneNumber { get; set; }
		public string? Email { get; set; }

		public string? Governorate { get; set; }
		public string? City { get; set; }
		public string? Street { get; set; }
		public string? Building { get; set; }
		public string? Floor { get; set; }
		public string? Apartment { get; set; }
		public string Ordernumber { get; set; }
		public string? State { get; set; }
		public string? PostalCode { get; set; }

		[Required]
        [Range(0.01, double.MaxValue, ErrorMessage = "Amount must be greater than zero.")]
        public decimal Amount { get; set; }
		public string? WalletPhoneNumber { get; set; }
		public PaymentMethodEnums PaymentMethod { get; set; }

        [StringLength(3, ErrorMessage = "Currency code should be 3 letters.")]
        public string Currency { get; set; } = "EGP";

        [StringLength(250)]
        public string? Notes { get; set; }
    }
    public class CreatePaymentOfCustomer
	{
     
       

		public string? WalletPhoneNumber { get; set; }
		public PaymentMethodEnums PaymentMethod { get; set; }

        [StringLength(3, ErrorMessage = "Currency code should be 3 letters.")]
        public string Currency { get; set; } = "EGP";

        [StringLength(250)]
        public string? Notes { get; set; }
    }
}




