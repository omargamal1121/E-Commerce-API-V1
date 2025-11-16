namespace DomainLayer.Enums
{
    public enum OrderStatus
    {
		PendingPayment = 0,
        Confirmed = 1,
        Processing = 2,
        Shipped = 3,
        Delivered = 4,
        CancelledByUser = 5,
        Refunded = 6,
        Returned = 7,
		PaymentExpired=8,
		CancelledByAdmin = 9,
        Complete = 10,
	}
} 