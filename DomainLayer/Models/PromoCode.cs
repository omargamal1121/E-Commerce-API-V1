using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Domain.Enums;

namespace Domain.Models
{
	public class PromoCode : BaseEntity
	{
		[Required]
		[StringLength(20, MinimumLength = 3, ErrorMessage = "Code must be between 3 and 20 characters")]
		[RegularExpression(@"^[A-Z0-9_-]{3,20}$", ErrorMessage = "Code can only contain uppercase letters, numbers, hyphens, and underscores.")]
		public string Code { get; set; } = string.Empty;

		[Range(0.01, 100.0, ErrorMessage = "Discount percent must be greater than 0 and less than or equal to 100.")]
		[Column(TypeName = "decimal(18,2)")]
		public decimal DiscountPercent { get; set; }

		[Required]
		public DateTime StartsAt { get; set; }

		[Required]
		public DateTime EndsAt { get; set; }

		public bool IsActive { get; set; } = false;

		[NotMapped]
		public PromoCodeStatus Status => ComputeStatus(DateTime.UtcNow);

		public PromoCodeStatus ComputeStatus(DateTime utcNow)
		{
			if (EndsAt <= utcNow) return PromoCodeStatus.Expired;
			if (StartsAt > utcNow) return PromoCodeStatus.Upcoming;
			if (!IsActive) return PromoCodeStatus.Inactive;
			return PromoCodeStatus.Active;
		}

		public ICollection<Order> Orders { get; set; } = new List<Order>();
	}
}
