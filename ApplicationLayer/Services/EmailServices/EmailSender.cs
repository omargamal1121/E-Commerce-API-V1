using Domain.Models;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.Extensions.Configuration;
using System.Net;
using System.Net.Mail;

using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;

namespace Application.Services.EmailServices

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

			var message = new MimeMessage();
			message.From.Add(MailboxAddress.Parse(from.Address));
			message.To.Add(MailboxAddress.Parse(email));
			message.Subject = subject;
			message.Body = new TextPart("html") { Text = htmlMessage };

			using var client = new MailKit.Net.Smtp.SmtpClient();
			try
			{
				await client.ConnectAsync(from.Host, from.Port, SecureSocketOptions.StartTls);
				await client.AuthenticateAsync(from.Address, from.Password);
				await client.SendAsync(message);
			}
			finally
			{
				await client.DisconnectAsync(true);
			}
		}
	}
	
	}


