using ApplicationLayer.DtoModels.Shared;
using ApplicationLayer.ErrorHnadling;
namespace ApplicationLayer.DtoModels.Responses
{
	public class ApiResponse<T> 
	{
		public int Statuscode { get; set; }
		
		public ResponseBody<T> ResponseBody { get; set; }
		public ApiResponse()
		{

		}
		public ApiResponse(int statuscode, ResponseBody<T> Response)
		{
			Statuscode = statuscode;
			ResponseBody = Response;
		}
		public static ApiResponse<T> CreateSuccessResponse(string message, T? data=default, int statusCode = 200, List<LinkDto>? links=null, List<string>? warnings=null)
		{
			return new ApiResponse<T>(statusCode, new ResponseBody<T>(message: message, data: data, links: links, warings: warnings));
		}
		public static ApiResponse<T> CreateSuccessWithWarnings(string message, List<string> warnings, T? data=default, int statusCode = 200, List<LinkDto>? links=null)
		{
			return new ApiResponse<T>(statusCode, new ResponseBody<T>(message: message, data: data, links: links, warings: warnings));
		}
		public static ApiResponse<T> CreateErrorResponse(string message,ErrorResponse error, int statusCode = 400, List<LinkDto>? links=null, List<string>? warnings=null)
		{
			var responsebody = new ResponseBody<T>(error: error, links: links, warings: warnings,message:message);
			return new ApiResponse<T>(statusCode, responsebody );
		}
	}
}

