using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace E_Commerce.Models
{
    public class PaymentWebhook : BaseEntity
    {
        [Required]
        public long TransactionId { get; set; }

        [Required]
		public long PaymobOrderId { get; set; }

		[Required]
        public int OrderId { get; set; }
        public Order Order { get; set; }
        
        [ForeignKey("Payment")]
        public int? PaymentId { get; set; }
        public Payment? Payment { get; set; }
        
        [Required]
        [StringLength(50)]
        public string PaymentMethod { get; set; } = string.Empty; // Visa, Wallet, Meeza, etc.
        
        [Required]
        public bool Success { get; set; }
        
        [Required]
        [StringLength(50)]
        public string Status { get; set; } = string.Empty; 
        
        [Required]
        public decimal AmountCents { get; set; }
        
        [Required]
        [StringLength(10)]
        public string Currency { get; set; } = "EGP";
        
        [StringLength(50)]
        public string? SourceSubType { get; set; } 
        
        [StringLength(100)]
        public string? SourceIssuer { get; set; } 
        
        [StringLength(20)]
        public string? CardLast4 { get; set; } // Last 4 digits of card
        
        [StringLength(50)]
        public string? PaymentProvider { get; set; } // PayMob, Stripe, etc.
        
        [StringLength(100)]
        public string? ProviderOrderId { get; set; } // PayMob order ID

        [Column(TypeName = "LONGTEXT")]
        public string? RawData { get; set; } // Full webhook payload for debugging

        [Required]
        public bool HmacVerified { get; set; } = false; 
        
        [StringLength(500)]
        public string? ErrorMessage { get; set; } // Error details if payment failed
        
        [StringLength(100)]
        public string? AuthorizationCode { get; set; } // Bank authorization code
        
        [StringLength(100)]
        public string? ReceiptNumber { get; set; } // Bank receipt number
        
        // Additional PayMob specific fields
        public bool? Is3DSecure { get; set; }
        public bool? IsCapture { get; set; }
        public bool? IsVoided { get; set; }
        public bool? IsRefunded { get; set; }
        
        [StringLength(50)]
        public string? IntegrationId { get; set; }
        
        [StringLength(50)]
        public string? ProfileId { get; set; }
        
        // Tracking fields
        public DateTime? ProcessedAt { get; set; }
        public int RetryCount { get; set; } = 0;
        
        [StringLength(100)]
        public string? WebhookUniqueKey { get; set; } // For idempotency
    }
}
