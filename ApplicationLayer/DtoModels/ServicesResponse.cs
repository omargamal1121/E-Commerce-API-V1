using ApplicationLayer.DtoModels.Shared;

namespace ApplicationLayer.DtoModels
{
	public class ServicesResponse(int statuscode,ResponseDto Response)
	{
		public static ServicesResponse CreateSuccessResponse(string message, object? data = null, int statusCode = 200)
		{
			return new ServicesResponse(statusCode, new ResponseDto(message, data, new List<LinkDto>()));
		}

		public static ServicesResponse CreateErrorResponse(string message, int statusCode = 400)
		{
			return new ServicesResponse(statusCode, new ResponseDto(message, links: new List<LinkDto>()));
		}
	}
}	


