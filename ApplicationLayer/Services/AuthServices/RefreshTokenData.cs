namespace Application.Services.AuthServices
{
	// Add this class to match your serialization/deserialization
	public class RefreshTokenData
    {
        public string UserId { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public DateTime ExpiresAt { get; set; }
    }
}