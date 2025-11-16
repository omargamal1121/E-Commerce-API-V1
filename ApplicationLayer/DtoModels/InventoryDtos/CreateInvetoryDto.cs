using System.ComponentModel.DataAnnotations;

namespace ApplicationLayer.DtoModels.InventoryDtos
{
	public class CreateInvetoryDto
	{

		[Range(0, int.MaxValue, ErrorMessage = $"Invalid Product Id ")]
		[Required(ErrorMessage = "Product Id Required")]

		public int ProductId { get; set; }
		[Required(ErrorMessage = "Quantity Required")]

		[Range(0, int.MaxValue, ErrorMessage = $"Invalid Quantity Id ")]
		public int Quantity { get; set; }
		[Required(ErrorMessage = "WareHouse Id Required")]

		[Range(0, int.MaxValue, ErrorMessage = $"Invalid WareHouse Id ")]
		public int WareHouseId { get; set; }
	}
}


