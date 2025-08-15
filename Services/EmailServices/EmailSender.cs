using E_Commerce.Models;
using Microsoft.AspNetCore.Identity.UI.Services;
using System.Net;
using System.Net.Mail;

namespace E_Commerce.Services.EmailServices

{
	public class EmailSender:IEmailSender
	{
		private readonly IConfiguration _configuration;
		public EmailSender(IConfiguration configuration)
		{
			_configuration = configuration;

		}
		private Email setdata()
		{
			return new Email
			{
				Address = _configuration["Email:Address"]??throw new Exception("Can't Find Emaill address"),
				Password = _configuration["Email:Password"] ?? throw new Exception("Can't Find Emaill password"),
				Host = _configuration["Email:Host"] ?? throw new Exception("Can't Find Emaill host"),
				Port = int.Parse(_configuration["Email:Port"] ?? throw new Exception("Can't Find Emaill port"))
			};
		}
		public async Task SendEmailAsync(string email, string subject, string htmlMessage)
		{
			Email from = setdata();
			MailMessage mailMessage = new MailMessage{
				From = new MailAddress(from.Address),
				Subject = subject,
				Body = $"<html><body> {htmlMessage}</body></html>",
				IsBodyHtml = true,

			};
			mailMessage.To.Add(email);
			try
			{
				using (SmtpClient smtpClient = new SmtpClient("smtp.gmail.com", 587))
				{
					smtpClient.Credentials = new NetworkCredential(from.Address, from.Password);
					smtpClient.EnableSsl = true;
					await smtpClient.SendMailAsync(mailMessage);
				}

			}
			catch (Exception ex)
			{

				throw new InvalidOperationException("Failed to send email.", ex);
			}
		
			

		}
	}
	
	}
