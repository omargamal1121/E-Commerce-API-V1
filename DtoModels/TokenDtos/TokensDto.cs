namespace E_Commerce.DtoModels.TokenDtos
{
	public class TokensDto
	{
		
		public string Token { get; set; }

		public TokensDto()
		{
			
		}
		public TokensDto(string userid,string token,string refreshtoken)
		{
		
			Token = token;
			
		}
	}
}
