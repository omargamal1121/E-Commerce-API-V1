using E_Commerce.DtoModels.Responses;
using E_Commerce.ErrorHnadling;
using E_Commerce.Models;
using E_Commerce.UOW;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using System.Collections.Generic;
using E_Commerce.DtoModels.PaymentDtos;
using E_Commerce.Services.PaymentWebhookService;

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
        public async Task<IActionResult> Receive([FromQuery]string HMAC,[FromBody] PaymobWebhookDto payload)
        {
            try
            {
                await _paymentWebhookService.HandlePaymobAsync(payload);
                return Ok();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing PayMob webhook");
                return StatusCode(500);
            }
        }
    }
}


