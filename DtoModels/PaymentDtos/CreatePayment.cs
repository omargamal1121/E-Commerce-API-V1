using E_Commerce.Enums;
using System.ComponentModel.DataAnnotations;

namespace E_Commerce.DtoModels.PaymentDtos
{
    public class CreatePayment
    {
        public string CustomerId { get; set; } = string.Empty;
        public int AddressId { get; set; }
        public string Ordernumber{ get; set; }

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


