using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace E_Commerce.Controllers
{
	public class RefreshTokenRequestDto
	{
		[Required(ErrorMessage = "RefreshToken is required")]
		[JsonPropertyName("refreshToken")]
		public string RefreshToken { get; set; } = string.Empty;
	}
}