using ApplicationLayer.DtoModels.Shared;

namespace ApplicationLayer.DtoModels.ProductDtos
{
	public class ReturnRequestProductDto : BaseDto
	{
		public int ReturnRequestId { get; set; }
		public int ProductId { get; set; }
		public int Quantity { get; set; }
		public string Reason { get; set; } = string.Empty;
		public ReturnStatus Status { get; set; }
	}
}


