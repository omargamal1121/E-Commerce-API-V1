using System.ComponentModel.DataAnnotations;
using E_Commerce.Enums;
using E_Commerce.Models;

namespace E_Commerce.DtoModels.ProductDtos
{
	public class CreateProductVariantDto
	{
		[Required(ErrorMessage = "Color is required.")]
		[StringLength(20, MinimumLength = 2, ErrorMessage = "Color must be between 2 and 20 characters.")]
		public string Color { get; set; } = string.Empty;

		public VariantSize? Size { get; set; }

		[Range(0, 100, ErrorMessage = "Waist must be between 0 and 100.")]
		public int? Waist { get; set; }

		[Range(0, 200, ErrorMessage = "Length must be between 0 and 200.")]
		public int? Length { get; set; }

		[Required(ErrorMessage = "Quantity is required.")]
		[Range(0, int.MaxValue, ErrorMessage = "Quantity must be non-negative.")]
		public int Quantity { get; set; }

	
	}

}