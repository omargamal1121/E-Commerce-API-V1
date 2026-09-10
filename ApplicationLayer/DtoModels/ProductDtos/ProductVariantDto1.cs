using Application.DtoModels.Shared;
using Domain.Enums;


namespace Application.DtoModels.ProductDtos
{
	public class ProductVariantDto : BaseDto
	{
		public string Color { get; set; } = string.Empty;
		public string? Size { get; set; }
		public int? Waist { get; set; }
		public int? Length { get; set; }
		public int? Chest { get; set; }
		public int? Hip { get; set; }
		public int Quantity { get; set; }
		public int ProductId { get; set; }
		public bool IsActive { get; set; }

	}
}


