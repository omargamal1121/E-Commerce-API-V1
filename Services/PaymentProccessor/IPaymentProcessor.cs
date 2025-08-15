using E_Commerce.DtoModels.PaymentDtos;
using E_Commerce.Services;

namespace E_Commerce.Services.PaymentProccessor
{
	public interface IPaymentProcessor
	{
		Task<Result<string>> GetPaymentLinkAsync(CreatePayment dto);
	}
}
