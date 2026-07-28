namespace Application.Services.AccountServices.Authentication
{
	internal class TokenPair
	{
		public string AccessToken { get; set; }
		public string RefreshToken { get; set; }
	}
}