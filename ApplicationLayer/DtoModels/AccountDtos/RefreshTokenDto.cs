using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace ApplicationLayer.DtoModels
{
	public class RefreshTokenDto
	{
		[Required(ErrorMessage = "UserId is required")]
		[JsonPropertyName("userId")]
		public Guid UserId { get; set; }

		[Required(ErrorMessage = "RefreshToken is required")]
		[JsonPropertyName("refreshToken")]
		public string RefreshToken { get; set; } = string.Empty;
	}

}


