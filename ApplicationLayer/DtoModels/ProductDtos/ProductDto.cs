using Application.DtoModels.Shared;

using System.ComponentModel.DataAnnotations;
using Application.DtoModels.InventoryDtos;
using Application.DtoModels.ImagesDtos;
using Application.DtoModels.Responses;
using Application.DtoModels.ProductDtos;
using Application.DtoModels.CollectionDtos;
using Application.DtoModels.SubCategorydto;
using Domain.Enums;
using Domain.Models;


namespace Application.DtoModels.ProductDtos
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

		public bool? DiscountStatus { get; set; }
		public DateTime? EndAt { get; set; }


		public bool IsActive { get; set; }
		public IEnumerable<ImageDto>? images { get; set; }
		public int TotalSold { get; set; }



	}

}


