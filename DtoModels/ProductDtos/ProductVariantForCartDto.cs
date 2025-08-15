using E_Commerce.DtoModels.Shared;
using E_Commerce.Enums;

namespace E_Commerce.DtoModels.ProductDtos
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
