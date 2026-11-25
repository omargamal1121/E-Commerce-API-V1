using System.Text.Json.Serialization;
using Domain.Enums;

namespace Application.DtoModels.PromoCodeDtos
{
    public class PromoCodeFilterDto
    {
        public string? Search { get; set; }

        [JsonConverter(typeof(JsonStringEnumConverter))]
        public PromoCodeStatus? Status { get; set; }

        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 10;
    }
}
