using Domain.Enums;
using System.ComponentModel.DataAnnotations;


namespace Application.DtoModels.ProductDtos
{
	public class UpdateProductVariantDto
	{
		

		public string? Color { get; set; }
		
		public string? Size { get; set; }
		public int? Waist { get; set; }

		public int? Length { get; set; }

		public int? Chest { get; set; }

		public int? Hip { get; set; }

		public int? SleeveLength { get; set; }

		public bool? IsFreeSize { get; set; }

	



	}
}

