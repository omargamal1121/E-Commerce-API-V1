

using DomainLayer.Enums;

namespace ApplicationLayer.DtoModels.OrderDtos
{
	public class OrderListDto
	{
		public int Id { get; set; }
		public string OrderNumber { get; set; } = string.Empty;
		public string CustomerName { get; set; } = string.Empty;
		public string Status { get; set; }
		public decimal Total { get; set; }
		public DateTime CreatedAt { get; set; }
		public DateTime ?UpdatedAt { get; set; }
		public string? imageurl { get; set; }
		public bool canBeCancelled { get; set; }
		public string paymentMethod { get; set; }
		public int itemCount { get; set; }
		public PaymentStatus  PaymentStatus { get; set; }
	}







}

