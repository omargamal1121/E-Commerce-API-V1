namespace Application.DtoModels.PromoCodeDtos
{
    public class PromoCodeValidationResultDto
    {
        public bool IsValid { get; set; }
        public string? Code { get; set; }
        public decimal? DiscountPercent { get; set; }
        public DateTime? EndsAt { get; set; }
        public string? Reason { get; set; }
    }

    public class ValidatePromoCodeRequestDto
    {
        public string Code { get; set; } = string.Empty;
    }
}
