namespace E_Commerce.DtoModels.PaymentDtos
{
    public class PaymentResponseDto
    {
        public bool IsRedirectRequired { get; set; }
        public string? RedirectUrl { get; set; }
        public string Message { get; set; } = string.Empty;
		public  int Paymentid { get; set; }
	}
}


