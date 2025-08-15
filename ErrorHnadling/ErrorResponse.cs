using System.Collections.Generic;

namespace E_Commerce.ErrorHnadling
{
	public class ErrorResponse
	{
		public string Title { get; set; }
		public List<string> Messages { get; set; }
		public string Detail { get; set; }
		public string Instance { get; set; }

		public ErrorResponse(string title, string message, string detail = null, string instance = null)
		{
			Title = title;
			Messages = new List<string> { message };
			Detail = detail;
			Instance = instance;
		}

		public ErrorResponse(string title, List<string> messages, string detail = null, string instance = null)
		{
			Title = title;
			Messages = messages;
			Detail = detail;
			Instance = instance;
		}
	}
}
