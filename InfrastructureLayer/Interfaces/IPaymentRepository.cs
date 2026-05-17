namespace Infrastructure.Interfaces
{
	public interface IPaymentRepository
	{
        public Task LockPaymentForUpdateAsync(int id);

    }
}