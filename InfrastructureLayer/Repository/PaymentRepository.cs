using Infrastructure.Interfaces;
using Domain.Models;
using Infrastructure.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Repository
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
        "SELECT Id FROM Payments WHERE Id = {0} FOR UPDATE",
       id);



        }

    }
}
