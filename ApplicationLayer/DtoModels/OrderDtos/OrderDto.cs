
using Application.DtoModels.CartDtos;
using Application.DtoModels.CustomerAddressDtos;
using Application.DtoModels.ImagesDtos;
using Application.DtoModels.ProductDtos;
using Application.DtoModels.Shared;
using Application.Services.PaymentMethodsServices;
using Application.Services.PaymentProvidersServices;
using Domain.Enums;

namespace Application.DtoModels.OrderDtos
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

    public class OrderAfterCreatedto
    {
        public string OrderNumber { get; set; } = string.Empty;
		public int OrderId { get; set; }
        public string? GuestToken { get; set; }

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
        // Snapshot fields — locked at order time, never change
        public string ProductName { get; set; } = string.Empty;
        public string? ImageUrl { get; set; }
        public string? Color { get; set; }
        public string? Size { get; set; }
        public int Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal TotalPrice { get; set; }
        public DateTime OrderedAt { get; set; }

        // Live availability flags only — do NOT use for display
        public bool IsProductAvailable { get; set; }
        public bool IsVariantAvailable { get; set; }
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
		public long  ProviderOrderId { get; set; }
	}

    
   





}

