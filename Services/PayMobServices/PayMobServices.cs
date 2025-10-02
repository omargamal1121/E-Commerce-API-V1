using E_Commerce.DtoModels.OrderDtos;
using E_Commerce.DtoModels.PaymentDtos;
using E_Commerce.Enums;
using E_Commerce.Models;
using E_Commerce.Services;
using E_Commerce.Services.EmailServices;
using E_Commerce.Services.PaymentProccessor;
using E_Commerce.UOW;
using Hangfire;
using Microsoft.AspNetCore.Authentication.OAuth;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using static E_Commerce.Services.PayMobServices.PayMobServices;
using static System.Net.WebRequestMethods;

namespace E_Commerce.Services.PayMobServices
{
	public interface IPayMobServices
	{
		Task<Result<PaymobPaymentStatusDto>> GetPaymentStatusAsync(long orderId);
		Task<Result<PaymentLinkResult>> GetPaymentLinkAsync(CreatePayment dto, int expires);
	}

	public class PayMobServices : IPaymentProcessor, IPayMobServices
	{
		private readonly IBackgroundJobClient _backgroundJobClient;
		private readonly ILogger<PayMobServices> _logger;
		private readonly IErrorNotificationService _errorNotificationService;
		private readonly IUnitOfWork _unitOfWork;
		private readonly UserManager<Customer> _userManager;
		private readonly HttpClient _httpClient;
		private IConfiguration _configuration;
		private readonly object _tokenLock = new object();
		private string _token = string.Empty;
		private DateTime _tokenGeneratedAt = DateTime.MinValue;

		public PayMobServices(
			IConfiguration configuration,
			UserManager<Customer> userManager, 
			IUnitOfWork unitOfWork, 
			ILogger<PayMobServices> logger, 
			IErrorNotificationService errorNotificationService, 
			IBackgroundJobClient backgroundJobClient,
			HttpClient httpClient)
		{
			_configuration = configuration;
			_userManager = userManager;
			_unitOfWork = unitOfWork;
			_logger = logger;
			_errorNotificationService = errorNotificationService;
			_backgroundJobClient = backgroundJobClient;
			_httpClient = httpClient;
		}

		private async Task<bool> GetTokenAsync()
		{
			lock (_tokenLock)
			{
				if (!string.IsNullOrEmpty(_token) && _tokenGeneratedAt.AddMinutes(55) > DateTime.UtcNow)
				{
					return true;
				}
			}

			try
			{
				var apiKey = await _unitOfWork.Repository<PaymentProvider>()
					.GetAll()
					.Where(p => p.Provider == PaymentProviderEnums.Paymob)
					.Select(p => p.PublicKey)
					.FirstOrDefaultAsync();

				if (string.IsNullOrEmpty(apiKey))
				{
					_logger.LogError("PayMob API key not found in database");
					_backgroundJobClient.Enqueue(() =>
						_errorNotificationService.SendErrorNotificationAsync("PayMob Configuration Error", "PayMob API key not found in database")
					);
					return false;
				}

				var body = new { api_key = apiKey };
				_logger.LogInformation("Executing GetTokenAsync");

				var json = JsonSerializer.Serialize(body);
				var content = new StringContent(json, Encoding.UTF8, "application/json");

				var response = await _httpClient.PostAsync("https://accept.paymob.com/api/auth/tokens", content);

				if (!response.IsSuccessStatusCode)
				{
					string error = await response.Content.ReadAsStringAsync();
					_backgroundJobClient.Enqueue(() =>
						_errorNotificationService.SendErrorNotificationAsync("PayMob - Failed to retrieve auth token", $"Status: {response.StatusCode}, Response: {error}")
					);
					_logger.LogError("PayMob - Failed to retrieve auth token. Status: {StatusCode}, Response: {Error}", response.StatusCode, error);
					return false;
				}

				var responseContent = await response.Content.ReadAsStringAsync();
				var result = JsonSerializer.Deserialize<TokenResponse>(
					responseContent,
					new JsonSerializerOptions { PropertyNameCaseInsensitive = true }
				);

				if (result == null || string.IsNullOrWhiteSpace(result.token))
				{
					_logger.LogError("Invalid token response from PayMob");
					_backgroundJobClient.Enqueue(() =>
						_errorNotificationService.SendErrorNotificationAsync("PayMob - Invalid token response", "Received null or empty token from PayMob")
					);
					return false;
				}

				lock (_tokenLock)
				{
					_token = result.token;
					_tokenGeneratedAt = DateTime.UtcNow;
				}

				_logger.LogInformation("Successfully retrieved PayMob token");
				return true;
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Exception occurred while retrieving PayMob token");
				_backgroundJobClient.Enqueue(() =>
					_errorNotificationService.SendErrorNotificationAsync("PayMob - Token retrieval exception", ex.Message)
				);
				return false;
			}
		}

		public async Task<Result<PaymobPaymentStatusDto>> GetPaymentStatusAsync(long orderId)
		{
			var tokenResult = await GetTokenAsync();
			if (!tokenResult)
			{
				_logger.LogError("Failed to get token for PayMob payment status check");
				return Result<PaymobPaymentStatusDto>.Fail("Failed to authenticate with Paymob");
			}

			try
			{
				_httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _token);

				var response = await _httpClient.GetAsync($"https://accept.paymob.com/api/ecommerce/orders/{orderId}");

				if (!response.IsSuccessStatusCode)
				{
					_logger.LogError("Paymob API call failed for order {OrderId}", orderId);
					return Result<PaymobPaymentStatusDto>.Fail("Failed to retrieve payment status");
				}

				var json = await response.Content.ReadAsStringAsync();
				using var doc = JsonDocument.Parse(json);

				var paidAmount = doc.RootElement.GetProperty("paid_amount_cents").GetInt32();
				var currency = doc.RootElement.TryGetProperty("currency", out var currencyEl) ? currencyEl.GetString() : "EGP";

				var status = (paidAmount > 0 ? "Paid" : "Unpaid");

				return Result<PaymobPaymentStatusDto>.Ok(new PaymobPaymentStatusDto
				{
					Status = status,
					PaidAmountCents = paidAmount,
					Currency = currency ?? "EGP"
				});
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Exception occurred while getting payment status for order {OrderId}", orderId);
				return Result<PaymobPaymentStatusDto>.Fail("Failed to retrieve payment status");
			}
		}

		private async Task<int> CreateOrderInPaymobAsync(CreateOrderRequest order)
		{
			var tokenResult = await GetTokenAsync();
			if (!tokenResult)
			{
				_logger.LogError("Failed to get token for PayMob order creation");
				return 0;
			}

			if (order == null)
			{
				_logger.LogError("CreateOrderInPaymobAsync received null order");
				return 0;
			}

			try
			{
				var json = JsonSerializer.Serialize(order);
				var content = new StringContent(json, Encoding.UTF8, "application/json");

				var response = await _httpClient.PostAsync("https://accept.paymob.com/api/ecommerce/orders", content);

				if (response.StatusCode == HttpStatusCode.Unauthorized)
				{
					// Try to refresh token and retry once
					var refreshResult = await GetTokenAsync();
					if (refreshResult)
					{
						response = await _httpClient.PostAsync("https://accept.paymob.com/api/ecommerce/orders", content);
					}
				}

				if (!response.IsSuccessStatusCode)
				{
					string error = await response.Content.ReadAsStringAsync();
					_backgroundJobClient.Enqueue(() => _errorNotificationService.SendErrorNotificationAsync("Error While creating order in paymob", error));
					_logger.LogError("Failed to create order in PayMob. Status: {StatusCode}, Error: {Error}", response.StatusCode, error);
					return 0;
				}

				var responseJson = await response.Content.ReadAsStringAsync();
				if (string.IsNullOrEmpty(responseJson))
				{
					_logger.LogError("Empty response from PayMob order creation");
					return 0;
				}

				var responseContent = JsonSerializer.Deserialize<CreateOrderResponse>(responseJson);
				if (responseContent == null)
				{
					_logger.LogError("Failed to deserialize PayMob order response");
					return 0;
				}

				return responseContent.id;
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Exception occurred while creating order in PayMob");
				return 0;
			}
		}

		private async Task<string?> GeneratePaymentKeyAsync(PaymentKeyContent content, PaymentMethodEnums paymentMethodEnums)
		{
			if (content == null)
			{
				_logger.LogError("GeneratePaymentKeyAsync received null content");
				return null;
			}

			var tokenResult = await GetTokenAsync();
			if (!tokenResult)
			{
				_logger.LogError("Failed to get token for payment key generation");
				return null;
			}

			try
			{
				_logger.LogInformation("Starting GeneratePaymentKeyAsync for Order ID: {OrderId}, Amount: {AmountCents}", content.order_id, content.amount_cents);

				var json = JsonSerializer.Serialize(content);
				var requestBody = new StringContent(json, Encoding.UTF8, "application/json");

				var response = await _httpClient.PostAsync("https://accept.paymob.com/api/acceptance/payment_keys", requestBody);

				_logger.LogInformation("Request sent to Paymob payment_keys endpoint");

				if (response.StatusCode == HttpStatusCode.Unauthorized)
				{
					var refreshResult = await GetTokenAsync();
					if (refreshResult)
					{
						response = await _httpClient.PostAsync("https://accept.paymob.com/api/acceptance/payment_keys", requestBody);
					}
				}

				if (!response.IsSuccessStatusCode)
				{
					string error = await response.Content.ReadAsStringAsync();
					_logger.LogError("Failed to get payment key from Paymob. Status: {StatusCode}, Response: {Error}", response.StatusCode, error);
					_backgroundJobClient.Enqueue(() => _errorNotificationService.SendErrorNotificationAsync("Error While getting payment key", error));
					return null;
				}

				var responseContent = await response.Content.ReadAsStringAsync();
				_logger.LogInformation("Received response from Paymob payment_keys endpoint");

				var result = JsonSerializer.Deserialize<TokenResponse>(responseContent,
					new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

				if (string.IsNullOrWhiteSpace(result?.token))
				{
					_logger.LogWarning("Payment key is null or empty in response");
					return null;
				}

				_logger.LogInformation("Payment key generated successfully for Order ID: {OrderId}", content.order_id);
				return result.token;
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Exception occurred while generating payment key for Order ID: {OrderId}", content.order_id);
				_backgroundJobClient.Enqueue(() => _errorNotificationService.SendErrorNotificationAsync("Exception while getting payment key", ex.Message));
				return null;
			}
		}

		public async Task<Result<PaymentLinkResult>> GetPaymentLinkAsync(CreatePayment dto, int expires)
		{
			if (dto == null)
			{
				return Result<PaymentLinkResult>.Fail("Invalid payment request", 400);
			}

			if (dto.WalletPhoneNumber == null && dto.PaymentMethod == PaymentMethodEnums.Wallet)
			{
				return Result<PaymentLinkResult>.Fail("Wallet Phone Number Needed", 400);
			}

			try
			{
				var tokenResult = await GetTokenAsync();
				if (!tokenResult)
				{
					_logger.LogError("Failed to get token for payment link generation");
					return Result<PaymentLinkResult>.Fail("Authentication failed", 401);
				}

				var user = await _userManager.FindByIdAsync(dto.CustomerId);
				if (user == null)
				{
					_logger.LogWarning("User not found for payment: {CustomerId}", dto.CustomerId);
					return Result<PaymentLinkResult>.Fail("User not found", 404);
				}

				var address = await _unitOfWork.CustomerAddress.GetAddressByIdAsync(dto.AddressId);
				if (address == null)
				{
					_logger.LogWarning("Address not found for payment: {AddressId}", dto.AddressId);
					return Result<PaymentLinkResult>.Fail("Address not found", 404);
				}

				var paymobOrderRequest = new CreateOrderRequest
				{
					auth_token = _token,
					amount_cents = (int)(dto.Amount * 100),
					currency = "EGP",
					delivery_needed = true,
                    merchant_order_id = dto.Ordernumber
				};

				var paymobOrderId = await CreateOrderInPaymobAsync(paymobOrderRequest);
				if (paymobOrderId == 0)
				{
					_logger.LogError("Failed to create order in PayMob for OrderId: {OrderId}", dto.Ordernumber);
					return Result<PaymentLinkResult>.Fail("Failed to create payment order", 500);
				}

				_logger.LogInformation("Successfully created PayMob order: {PayMobOrderId} for local order: {OrderId}", paymobOrderId, dto.Ordernumber);

				var amountInCents = (int)(dto.Amount * 100);
				var integrationId = await _unitOfWork.Repository<PaymentMethod>()
					.GetAll()
					.Where(p => p.Method == dto.PaymentMethod && p.IsActive && p.PaymentProviders.Provider == PaymentProviderEnums.Paymob)
					.Select(p => p.IntegrationId)
					.FirstOrDefaultAsync();

				if (string.IsNullOrEmpty(integrationId))
				{
					_logger.LogError("Integration ID not found for payment method: {PaymentMethod}. Please configure the integration ID in the database.", dto.PaymentMethod);
					return Result<PaymentLinkResult>.Fail("Payment method not configured", 400);
				}

				_logger.LogInformation("Using integration ID: {IntegrationId} for payment method: {PaymentMethod}", integrationId, dto.PaymentMethod);

				string redirection_url = _configuration["Security:Paymob:redirection_url"]??"";
				_logger.LogInformation(redirection_url);

                var paymentKeyRequest = new PaymentKeyContent
				{
					amount_cents = amountInCents,
					auth_token = _token,
					expiration = expires,
					order_id = paymobOrderId,
					integration_id = integrationId,
					redirection_url=redirection_url,
					billing_data = new billing_data
					{
						city = address.City ?? "NA",
						country = address.Country ?? "EG",
						state = address.State ?? "NA",
						postal_code = address.PostalCode ?? "NA",
						street = address.StreetAddress,
						first_name = user.Name?.Split(" ").FirstOrDefault() ?? "NA",
						last_name = user.Name?.Split(" ").Skip(1).FirstOrDefault() ?? "NA",
						email = user.Email,
						phone_number = user.PhoneNumber
					}
					,

				};

				var paymentKey = await GeneratePaymentKeyAsync(paymentKeyRequest, dto.PaymentMethod);
				if (string.IsNullOrEmpty(paymentKey))
				{
					_logger.LogError("Failed to generate payment key from PayMob for order: {OrderId}", dto.Ordernumber);
					return Result<PaymentLinkResult>.Fail("Failed to generate payment key", 500);
				}

				_logger.LogInformation("Successfully generated payment key for order: {OrderId}", dto.Ordernumber);

				string paymentUrl;

				if (dto.PaymentMethod == PaymentMethodEnums.Wallet)
				{
					paymentUrl = await WalletUrl(PaymentProviderEnums.Paymob, paymentKey, dto.WalletPhoneNumber);
					if (string.IsNullOrEmpty(paymentUrl))
					{
						_logger.LogWarning("Can't Generate Link Of Wallet.. maybe number doesn't have wallet");
						return Result<PaymentLinkResult>.Fail("Please Check If you Have Wallet on this number");
					}
				}
				else
				{
					paymentUrl = await OnlineCardUrl(PaymentProviderEnums.Paymob, paymentKey);
				}

				return Result<PaymentLinkResult>.Ok(new PaymentLinkResult { PaymentUrl = paymentUrl, PaymobOrderId = paymobOrderId });
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Exception occurred while generating payment link");
				return Result<PaymentLinkResult>.Fail("Failed to initiate payment", 500);
			}
		}

		private async Task<string> WalletUrl(PaymentProviderEnums paymentProvider, string paymentKey, string phoneNumber)
		{
			var walletRequest = new
			{
				source = new
				{
					identifier = phoneNumber,
					subtype = "WALLET"
				},
				payment_token = paymentKey
			};

			var response = await _httpClient.PostAsync(
				"https://accept.paymob.com/api/acceptance/payments/pay",
				new StringContent(JsonSerializer.Serialize(walletRequest), Encoding.UTF8, "application/json")
			);

			var content = await response.Content.ReadAsStringAsync();

			if (!response.IsSuccessStatusCode)
			{
				_logger.LogError("Failed to initiate wallet payment. Status: {StatusCode}, Response: {Error}", response.StatusCode, content);
				throw new Exception("Failed to initiate wallet payment");
			}

			var payResult = JsonSerializer.Deserialize<PaymobWalletResponse>(content,
				new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

			return payResult?.redirect_url ?? throw new Exception("Wallet redirect_url not found");
		}

		private async Task<string> OnlineCardUrl(PaymentProviderEnums paymentProvider, string paymentKey)
		{
			var iframeId = await _unitOfWork.Repository<PaymentProvider>()
				   .GetAll()
				   .Where(p => p.Provider == paymentProvider)
				   .Select(p => p.IframeId)
				   .FirstOrDefaultAsync();

			if (string.IsNullOrWhiteSpace(iframeId))
			{
				_logger.LogWarning("Paymob IframeId not configured. Falling back to default.");
				iframeId = "0";
			}
			return $"https://accept.paymob.com/api/acceptance/iframes/{iframeId}?payment_token={paymentKey}";
		}

		public class PaymobWalletResponse
		{
			public string redirect_url { get; set; }
		}


		public class CreateOrderRequest
		{
			public bool delivery_needed { get; set; }
			public decimal amount_cents { get; set; }
			public string currency { get; set; } = "EGP";
			public string auth_token { get; set; } = string.Empty;
            public string? merchant_order_id { get; set; }
		}

		public class PaymentKeyContent
		{
			public string currency { get; set; } = "EGP";
			public string auth_token { get; set; } = string.Empty;
			public decimal amount_cents { get; set; }
			public int expiration { get; set; } = 1000;
			public int order_id { get; set; }
			public string integration_id { get; set; } = string.Empty;
			public string redirection_url { get; set; }
			public billing_data billing_data { get; set; } = new billing_data();
		}
		public class PaymentLinkResult
		{
			public string PaymentUrl { get; set; }
			public long PaymobOrderId { get; set; }
		}
		public class PaymobPaymentStatusDto
		{
			public string Status { get; set; } = "Unpaid"; // Paid, Pending, Unpaid
			public int PaidAmountCents { get; set; }
			public string Currency { get; set; } = "EGP";
		}

		public class billing_data
		{
			public string apartment { get; set; } = "NA";
			public string phone_number { get; set; } = "NA";
			public string email { get; set; } = string.Empty;
			public string floor { get; set; } = "NA";
			public string first_name { get; set; } = string.Empty;
			public string street { get; set; } = "NA";
			public string building { get; set; } = "NA";
			public string shipping_method { get; set; } = "NA";
			public string postal_code { get; set; } = "NA";
			public string city { get; set; } = "NA";
			public string country { get; set; } = "EG";
			public string last_name { get; set; } = string.Empty;
			public string state { get; set; } = "NA";
		}

		public class CreateOrderResponse
		{
			public int id { get; set; }
			public DateTime created_at { get; set; }
			public decimal amount_cents { get; set; }
			public string currency { get; set; } = "EGP";
		}

		public class TokenResponse
		{
			public string token { get; set; } = string.Empty;
		}
	}
}

