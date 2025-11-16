using ApplicationLayer.DtoModels.Shared;
using DomainLayer.Enums;


namespace ApplicationLayer.DtoModels.ProductDtos
{
	public class ProductVariantDto : BaseDto
	{
		public string Color { get; set; } = string.Empty;
		public VariantSize? Size { get; set; }
		public int? Waist { get; set; }
		public int? Length { get; set; }
		public int Quantity { get; set; }
		public int ProductId { get; set; }
		public bool IsActive { get; set; }

	}
}


