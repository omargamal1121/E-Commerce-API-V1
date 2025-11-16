using System.ComponentModel.DataAnnotations;

namespace ApplicationLayer.DtoModels.InventoryDtos
{
	public class AddQuantityInvetoryDto
	{
		[Required(ErrorMessage = "Invetory Id Required")]
		public int Id { get; set; }
  
		[Required(ErrorMessage = "Quantity Required")]

		[Range(0, int.MaxValue, ErrorMessage = $"Invalid Quantity Id ")]
		public int Quantity { get; set; }
	}
}


