using Application.DtoModels.PaymentDtos;
using Application.Services;
using static Application.Services.PayMobServices.PayMobServices;


namespace Application.Services.PaymentProccessor
{
	public interface IPaymentProcessor
	{
		Task<Result<PaymentLinkResult>> GetPaymentLinkAsync(CreatePayment dto, int expries, long? orderproviderid=null);
		 Task<Result<PaymobPaymentStatusDto>> GetPaymentStatusAsync(long orderId);
	}
}


