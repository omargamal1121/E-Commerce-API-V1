namespace ApplicationLayer.Interfaces
{
	public interface IPaymentRepository
	{
        public Task LockPaymentForUpdateAsync(int id);

    }
}