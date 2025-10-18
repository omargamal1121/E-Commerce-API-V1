using E_Commerce.DtoModels.Shared;
using E_Commerce.Models;
using System.ComponentModel.DataAnnotations;
using E_Commerce.DtoModels.InventoryDtos;
using E_Commerce.DtoModels.ImagesDtos;
using E_Commerce.DtoModels.Responses;
using E_Commerce.DtoModels.ProductDtos;
using E_Commerce.Enums;
using E_Commerce.DtoModels.CollectionDtos;
using E_Commerce.DtoModels.SubCategorydto;

namespace E_Commerce.DtoModels.ProductDtos
{
	public class ProductDto:BaseDto
	{
		[RegularExpression(@"^[a-zA-Z0-9][a-zA-Z0-9\s\-,]*[a-zA-Z0-9]$", ErrorMessage = "Name must start and end with an alphanumeric character and can contain spaces, hyphens, and commas in between.")]
		public string Name { get; set; } = string.Empty;
		
		[RegularExpression(@"^[\w\s.,\-()'\""]{0,500}$", ErrorMessage = "Description can contain up to 500 characters: letters, numbers, spaces, and .,-()'\"")]
		public string Description { get; set; } = string.Empty;
		
		public int SubCategoryId { get; set; }
		public int AvailableQuantity { get; set; }
		public decimal Price { get; set; }
		public decimal? FinalPrice { get; set; }
		public Gender Gender { get; set; }
		public FitType  fitType { get; set; }
		public decimal? DiscountPrecentage { get; set; }
		public string? DiscountName { get; set; }

		public DateTime? EndAt { get; set; }
		public bool IsInWithList { get; set; }


		public bool IsActive { get; set; }
		public IEnumerable<ImageDto>? images { get; set; }



	}
}
