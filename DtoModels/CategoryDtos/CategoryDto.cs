using E_Commerce.DtoModels.ImagesDtos;
using E_Commerce.DtoModels.ProductDtos;
using E_Commerce.DtoModels.Shared;
using E_Commerce.Models;
using System.ComponentModel.DataAnnotations;

namespace E_Commerce.DtoModels.CategoryDtos
{
	public class CategoryDto:BaseDto
	{
		[RegularExpression(@"^[a-zA-Z0-9][a-zA-Z0-9\s\-,]*[a-zA-Z0-9]$", ErrorMessage = "Name must start and end with an alphanumeric character and can contain spaces, hyphens, and commas in between.")]
		public string Name { get; set; } = string.Empty;
		[RegularExpression(@"^[\w\s.,\-()'\""]{0,500}$", ErrorMessage = "Description can contain up to 500 characters: letters, numbers, spaces, and .,-()'\"")]
		public string Description { get; set; } = string.Empty;
		public int DisplayOrder { get; set; }
		public List<ImageDto> Images { get; set; } = new List<ImageDto>();
		public bool IsActive { get; set; }
	}
}
