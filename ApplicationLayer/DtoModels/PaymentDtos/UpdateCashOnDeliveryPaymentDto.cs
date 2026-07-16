using System.ComponentModel.DataAnnotations;

namespace Application.DtoModels.PaymentDtos
{
    public class UpdateCashOnDeliveryPaymentDto
    {
        [Required]
        public int PaymentId { get; set; }

        [Required]
        [StringLength(100, MinimumLength = 3)]
        public string? TransactionId { get; set; }
    }
}
