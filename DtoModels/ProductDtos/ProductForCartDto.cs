namespace E_Commerce.DtoModels.ProductDtos
{
	public class ProductForCartDto
	{
		public int Id { get; set; }

		public string Name { get; set; } = string.Empty;

		public decimal Price { get; set; }

		public decimal FinalPrice { get; set; }

		public string? DiscountName { get; set; }

		public decimal? DiscountPrecentage { get; set; }

		public string? MainImageUrl { get; set; } 

		public bool IsActive { get; set; }

		public ProductVariantForCartDto  productVariantForCartDto { get; set; }
	}
}
