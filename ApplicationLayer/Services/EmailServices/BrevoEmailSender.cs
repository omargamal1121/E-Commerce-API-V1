using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.Extensions.Configuration;
using System.Net.Http.Json;

namespace Application.Services.EmailServices
{
	public class BrevoEmailSender : IEmailSender
	{
		private readonly HttpClient _httpClient;
		private readonly IConfiguration _configuration;

		public BrevoEmailSender(HttpClient httpClient, IConfiguration configuration)
		{
			_httpClient = httpClient;
			_configuration = configuration;
		}

		public async Task SendEmailAsync(string email, string subject, string htmlMessage)
		{
			var apiKey = _configuration["Brevo:ApiKey"]
				?? throw new Exception("Brevo API key missing");

			var payload = new
			{
				sender = new { email = _configuration["Brevo:SenderEmail"], name = "R&S" },
				to = new[] { new { email } },
				subject,
				htmlContent = $"<html><body>{htmlMessage}</body></html>"
			};

			var request = new HttpRequestMessage(HttpMethod.Post, "https://api.brevo.com/v3/smtp/email")
			{
				Content = JsonContent.Create(payload)
			};
			request.Headers.Add("api-key", apiKey);

			var response = await _httpClient.SendAsync(request);

			if (!response.IsSuccessStatusCode)
			{
				var error = await response.Content.ReadAsStringAsync();
				throw new InvalidOperationException($"Brevo email failed: {error}");
			}
		}
	}
}
