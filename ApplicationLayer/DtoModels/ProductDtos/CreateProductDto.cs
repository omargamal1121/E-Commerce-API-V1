using System.ComponentModel.DataAnnotations;
using Domain.Enums;
using Domain.Models;
using Microsoft.AspNetCore.Http;



namespace Application.DtoModels.ProductDtos
{
	public class CreateProductDto 
	{
		[Required(ErrorMessage = "Name is required.")]
		public string Name { get; set; } = string.Empty;

		[Required(ErrorMessage = "Description is required.")]
		public string Description { get; set; } = string.Empty;

		public int Subcategoryid { get; set; }
		public string? fitType { get; set; }



	
		public Gender Gender { get; set; }
		[Range(100,float.MaxValue)]
		public decimal Price { get; set; }



	}
}


