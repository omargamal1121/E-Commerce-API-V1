using System.ComponentModel.DataAnnotations;

namespace Application.DtoModels.PromoCodeDtos
{
    public class CreatePromoCodeDto
    {
        [Required(ErrorMessage = "Promo code is required")]
        [StringLength(20, MinimumLength = 3, ErrorMessage = "Promo code must be between 3 and 20 characters")]
        [RegularExpression(@"^[A-Za-z0-9_-]{3,20}$", ErrorMessage = "Promo code can only contain letters, numbers, hyphens, and underscores.")]
        public string Code { get; set; } = string.Empty;

        [Required(ErrorMessage = "Discount percent is required")]
        [Range(0.01, 100.0, ErrorMessage = "Discount percent must be greater than 0 and less than or equal to 100.")]
        public decimal DiscountPercent { get; set; }

        [Required(ErrorMessage = "Start date is required")]
        public DateTime StartsAt { get; set; }

        [Required(ErrorMessage = "End date is required")]
        public DateTime EndsAt { get; set; }

        public bool IsActive { get; set; } = false;
    }
}
