namespace ApplicationLayer.Services.AuthServices
{
	// Add this class to match your serialization/deserialization
	public class RefreshTokenData
    {
        public string UserId { get; set; } = string.Empty;
        public string SecurityStamp { get; set; } = string.Empty;
    }
}