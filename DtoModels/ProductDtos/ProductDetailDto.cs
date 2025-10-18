using E_Commerce.DtoModels.DiscoutDtos;
using E_Commerce.DtoModels.Shared;
using E_Commerce.Models;
using E_Commerce.DtoModels.ImagesDtos;
using E_Commerce.Enums;

namespace E_Commerce.DtoModels.ProductDtos
{
	public class ProductDetailDto : BaseDto
	{
		public string Name { get; set; } = string.Empty;
		public string Description { get; set; } = string.Empty;
		public int SubCategoryId { get; set; }
		public DiscountDto? Discount { get; set; }
		public int AvailableQuantity { get; set; }
		public decimal Price { get; set; }
		public Gender Gender { get; set; }
		public bool IsActive { get; set; }
		public  FitType fitType { get; set; }

		public IEnumerable<ImageDto>? Images { get; set; }
		public IEnumerable<ProductVariantDto>? Variants { get; set; }
		public decimal? FinalPrice { get; set; }
		public bool InWishList { get; set; }
	}
}
