namespace Application.Services.PromoCodeServices
{
    public class PromoCodeValidationResult
    {
        public bool IsValid { get; set; }
        public int? PromoCodeId { get; set; }
        public string? Code { get; set; }
        public decimal? DiscountPercent { get; set; }
        public DateTime? EndsAt { get; set; }
        public string? Reason { get; set; }

        public static PromoCodeValidationResult Success(int id, string code, decimal discountPercent, DateTime endsAt) =>
            new PromoCodeValidationResult
            {
                IsValid = true,
                PromoCodeId = id,
                Code = code,
                DiscountPercent = discountPercent,
                EndsAt = endsAt
            };

        public static PromoCodeValidationResult Failure(string reason) =>
            new PromoCodeValidationResult
            {
                IsValid = false,
                Reason = reason
            };
    }
}
