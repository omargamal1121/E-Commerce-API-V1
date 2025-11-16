using ApplicationLayer.Interfaces;
using DomainLayer.Models;
using InfrastructureLayer.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace InfrastructureLayer.Repository
{
	public class PaymentRepository : MainRepository<Payment>, IPaymentRepository
	{
		private readonly ILogger<PaymentRepository> _logger;
		private readonly AppDbContext _context;
        public PaymentRepository(AppDbContext context, ILogger<PaymentRepository> logger) : base(context, logger)
		{
			_context = context;
			_logger = logger;
        }

        public async Task LockPaymentForUpdateAsync(int id)
		{
			_logger.LogInformation($"Locking payment with ID: {id} for update.");
			var payment = await _context.Database.ExecuteSqlRawAsync(
        "SELECT Id FROM Orders WHERE Id = {0} FOR UPDATE",
       id);



        }

    }
}
