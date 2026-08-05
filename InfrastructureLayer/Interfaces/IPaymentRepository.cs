using Domain.Models;

namespace Infrastructure.Interfaces
{
	public interface IPaymentRepository
	{
        public Task LockPaymentForUpdateAsync(int id);
		public Task<Payment?> GetCODPayment(int id);

    }
}