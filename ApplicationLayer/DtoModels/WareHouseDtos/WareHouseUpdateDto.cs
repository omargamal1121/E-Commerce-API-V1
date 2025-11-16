using System.ComponentModel.DataAnnotations;

namespace ApplicationLayer.DtoModels.WareHouseDtos
{
	public class UpdateWareHouseDto
	{

		[StringLength(20, MinimumLength = 5, ErrorMessage = "Must Between 5 t0 20 ")]
		[RegularExpression(@"^[a-zA-Z0-9][a-zA-Z0-9\s\-,]*[a-zA-Z0-9]$", ErrorMessage = "Name must start and end with an alphanumeric character and can contain spaces, hyphens, and commas in between.")]
		public string? NewName { get; set; } 
		[StringLength(50, MinimumLength = 10, ErrorMessage = "Must Between 10 t0 50 ")]
		public string? NewAddress { get; set; } 
		[Phone]
		public string? NewPhone { get; set; }
	}
}


