namespace E_Commerce.Enums
{
    public enum OrderStatus
    {
		PendingPayment = 0,
        Confirmed = 1,
        Processing = 2,
        Shipped = 3,
        Delivered = 4,
        Cancelled = 5,
        Refunded = 6,
        Returned = 7,
		PaymentExpired=8
	}
} 