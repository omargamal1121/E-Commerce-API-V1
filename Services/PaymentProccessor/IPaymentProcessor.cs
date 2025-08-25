using E_Commerce.DtoModels.PaymentDtos;
using E_Commerce.Services;
using static E_Commerce.Services.PayMobServices.PayMobServices;

namespace E_Commerce.Services.PaymentProccessor
{
	public interface IPaymentProcessor
	{
		Task<Result<PaymentLinkResult>> GetPaymentLinkAsync(CreatePayment dto, int expries);
		 Task<Result<PaymobPaymentStatusDto>> GetPaymentStatusAsync(long orderId);
	}
}
