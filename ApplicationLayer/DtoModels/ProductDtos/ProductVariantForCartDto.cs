using Application.DtoModels.Shared;
using Domain.Enums;


namespace Application.DtoModels.ProductDtos
{
	public class ProductVariantForCartDto : BaseDto
	{
		public string Color { get; set; } = string.Empty;
		public VariantSize? Size { get; set; }
		public int? Waist { get; set; }
		public int? Length { get; set; }
		public int Quantity { get; set; }

	}
}


