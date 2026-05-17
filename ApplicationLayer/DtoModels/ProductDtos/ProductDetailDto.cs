using Application.DtoModels.DiscoutDtos;
using Application.DtoModels.Shared;

using Application.DtoModels.ImagesDtos;
using Domain.Models;
using Domain.Enums;


namespace Application.DtoModels.ProductDtos
{
	public class ProductDetailDto : BaseDto
	{
		public string Name { get; set; }
		public string Description { get; set; }
		public int SubCategoryId { get; set; }
		public DiscountDto? Discount { get; set; }
		public int AvailableQuantity { get; set; }
		public decimal Price { get; set; }
		public Gender Gender { get; set; }
		public bool IsActive { get; set; }
		public  FitType fitType { get; set; }
		public string SubCategoryName { get; set; }
	
		public IEnumerable<ImageDto>? Images { get; set; }
		public IEnumerable<ProductVariantDto>? Variants { get; set; }
		public decimal? FinalPrice { get; set; }
	}


}


