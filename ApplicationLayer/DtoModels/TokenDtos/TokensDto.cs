namespace Application.DtoModels.TokenDtos
{
	public class TokensDto
	{
		
		public string Token { get; set; }
		public string RefreshToken { get; set; }
		public List<string> Roles { get; set; }	

        public TokensDto()
		{
			
		}
		
	}
}


