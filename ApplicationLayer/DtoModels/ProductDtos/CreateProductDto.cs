using System.ComponentModel.DataAnnotations;
using DomainLayer.Enums;
using DomainLayer.Models;
using Microsoft.AspNetCore.Http;



namespace ApplicationLayer.DtoModels.ProductDtos
{
	public class CreateProductDto 
	{
		[Required(ErrorMessage = "Name is required.")]
		[StringLength(20, MinimumLength = 5, ErrorMessage = "Name must be between 5 and 20 characters.")]
		[RegularExpression(@"^[a-zA-Z0-9][a-zA-Z0-9\s\-,]*[a-zA-Z0-9]$", ErrorMessage = "Name must start and end with an alphanumeric character and can contain spaces, hyphens, and commas in between.")]
		public string Name { get; set; } = string.Empty;

		[Required(ErrorMessage = "Description is required.")]
		[StringLength(50, MinimumLength = 10, ErrorMessage = "Description must be between 10 and 50 characters.")]
		public string Description { get; set; } = string.Empty;

		public int Subcategoryid { get; set; }
		public FitType  fitType { get; set; }



		[Required(ErrorMessage = "Gender is required")]
		[Range(1, 4, ErrorMessage = "Gender must be between 1 and 4")]
		public Gender Gender { get; set; }
		[Range(100,float.MaxValue)]
		public decimal Price { get; set; }



	}
}


