using ApplicationLayer.DtoModels.PaymentDtos;
using ApplicationLayer.Services;
using static ApplicationLayer.Services.PayMobServices.PayMobServices;


namespace ApplicationLayer.Services.PaymentProccessor
{
	public interface IPaymentProcessor
	{
		Task<Result<PaymentLinkResult>> GetPaymentLinkAsync(CreatePayment dto, int expries);
		 Task<Result<PaymobPaymentStatusDto>> GetPaymentStatusAsync(long orderId);
	}
}


