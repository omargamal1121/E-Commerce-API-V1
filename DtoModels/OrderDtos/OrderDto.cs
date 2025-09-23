using E_Commerce.DtoModels.CartDtos;
using E_Commerce.DtoModels.CustomerAddressDtos;
using E_Commerce.DtoModels.ImagesDtos;
using E_Commerce.DtoModels.ProductDtos;
using E_Commerce.DtoModels.Shared;
using E_Commerce.Enums;
using E_Commerce.Services.PaymentMethodsServices;
using E_Commerce.Services.PaymentProvidersServices;

namespace E_Commerce.DtoModels.OrderDtos
{
    public class OrderDto : BaseDto
    {
        public string OrderNumber { get; set; } = string.Empty;
        public CustomerDto? Customer { get; set; }
        public string Status { get; set; }
        public decimal Subtotal { get; set; }
        public decimal TaxAmount { get; set; }
        public decimal ShippingCost { get; set; }
        public decimal DiscountAmount { get; set; }
        public decimal Total { get; set; }
        public string? Notes { get; set; }
        public DateTime? ShippedAt { get; set; }
        public DateTime? DeliveredAt { get; set; }
        public DateTime? CancelledAt { get; set; }
        public List<OrderItemDto> Items { get; set; } = new List<OrderItemDto>();
        public IEnumerable<PaymentDto>? Payment { get; set; }
        
        public bool IsCancelled => Status == OrderStatus.CancelledByUser.ToString()|| Status == OrderStatus.CancelledByAdmin.ToString();
        public bool IsDelivered => Status == OrderStatus.Delivered.ToString();
        public bool IsShipped => Status == OrderStatus.Shipped.ToString();
        public bool CanBeCancelled => Status == OrderStatus.PendingPayment.ToString() || Status == OrderStatus.Confirmed.ToString();
        public bool CanBeReturned => Status == OrderStatus.Delivered.ToString();
        public string StatusDisplay => Status.ToString();
    }
	public class OrderWithPaymentDto
	{
		public OrderDto Order { get; set; }
		public string? PaymentUrl { get; set; } 
	}

	public class CustomerDto
	{

		public string Id { get; set; } = string.Empty;
		public string FullName { get; set; } = string.Empty;
		public string Email { get; set; } = string.Empty;
		public string PhoneNumber { get; set; } = string.Empty;

		public  CustomerAddressDto customerAddress { get; set; }

	}

	public class OrderItemDto : BaseDto
    {

        public ProductForCartDto Product { get; set; }

      
        public int Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal TotalPrice { get; set; }
        public DateTime OrderedAt { get; set; }
    }

    public class PaymentDto : BaseDto
    {
        public string CustomerId { get; set; } = string.Empty;
        public int PaymentMethodId { get; set; }
        public string? PaymentMethod { get; set; }
        public int PaymentProviderId { get; set; }
        public PaymentProviderDto? PaymentProvider { get; set; }
        public decimal Amount { get; set; }
        public DateTime PaymentDate { get; set; }
        public int OrderId { get; set; }
        public string Status { get; set; } 
    }

    
   





}