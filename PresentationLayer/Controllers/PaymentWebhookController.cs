using Infrastructure.Interfaces;
using Application.DtoModels.PaymentDtos;
using Application.Interfaces;
using Application.Services.PaymentWebhookService;
using Infrastructure.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text.Json;

namespace E_Commerce.Controllers
{
    [ApiController]
    [Route("api/payments/webhook/paymob")] // adjust route as needed
    public class PaymentWebhookController : ControllerBase
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly ILogger<PaymentWebhookController> _logger;
        private readonly IPaymentWebhookService _paymentWebhookService;

        public PaymentWebhookController(IUnitOfWork unitOfWork, ILogger<PaymentWebhookController> logger, IPaymentWebhookService paymentWebhookService)
        {
            _unitOfWork = unitOfWork;
            _logger = logger;
            _paymentWebhookService = paymentWebhookService;
        }

        [HttpPost]
        public async Task<IActionResult> Receive(
            [FromQuery] string? hmac, 
            [FromBody] PaymobWebhookDto? payload)
         {
            try
            {
                
                if (string.IsNullOrWhiteSpace(hmac))
                {
                    return BadRequest("Missing HMAC parameter");
                }

                if (payload == null)
                {
                    return BadRequest("Invalid payload");
                }
                bool success = await _paymentWebhookService.HandlePaymobAsync(payload, hmac);

                return success ? Ok() : BadRequest("Webhook processing failed");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing PayMob webhook");
                return StatusCode(500);
            }
        }
    }
}


