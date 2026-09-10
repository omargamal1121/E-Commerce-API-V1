using System.ComponentModel.DataAnnotations;
using Domain.Enums;
using Domain.Models;
using Microsoft.AspNetCore.Http;

namespace Application.DtoModels.ProductDtos
{
	public class UpdateProductDto
	{
		public string? Name { get; set; }
	public string? Description { get; set; }

		public int? SubCategoryid { get; set; }
		[Range(100,float.MaxValue)]
		public  decimal? Price { get; set; }
		public string? fitType { get; set; }
		public Gender? Gender { get; set; }
	}
}


