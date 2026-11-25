using System.Text.Json.Serialization;
using Domain.Enums;

namespace Application.DtoModels.PromoCodeDtos
{
    public class PromoCodeDto
    {
        public int Id { get; set; }
        public string Code { get; set; } = string.Empty;
        public decimal DiscountPercent { get; set; }
        public DateTime StartsAt { get; set; }
        public DateTime EndsAt { get; set; }
        public bool IsActive { get; set; }

        [JsonConverter(typeof(JsonStringEnumConverter))]
        public PromoCodeStatus Status { get; set; }

        public int OrdersCount { get; set; }
        public DateTime? CreatedAt { get; set; }
        public DateTime? ModifiedAt { get; set; }
    }
}
