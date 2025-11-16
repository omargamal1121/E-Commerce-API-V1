

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
	}







}

