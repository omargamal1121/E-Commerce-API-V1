namespace E_Commerce.Services
{
	public class Result<T>
	{
		public bool Success { get; set; }
		public string Message { get; set; } = string.Empty;
		public T? Data { get; set; }
		public List<string>? Warnings { get; set; }
		public int StatusCode { get; set; }

		public static Result<T> Fail(string message, int statusCode = 400, List<string>? warnings = null) => new Result<T> { Success = false, Message = message, StatusCode = statusCode, Warnings = warnings };
		public static Result<T> Fail(string message, T data, int statusCode = 400, List<string>? warnings = null) => new Result<T> { Success = false, Message = message, Data = data, StatusCode = statusCode, Warnings = warnings };
		public static Result<T> Ok(T data, string message = "Operation succeeded", int statusCode = 200, List<string>? warnings = null)
			=> new Result<T> { Success = true, Data = data, Message = message, StatusCode = statusCode, Warnings = warnings };
	}
}
