using ApplicationLayer.DtoModels.ProductDtos;
using ApplicationLayer.DtoModels.Shared;

namespace ApplicationLayer.DtoModels.InventoryDtos
{
	public class InventoryDto:BaseDto
	{
		public int Quantityinsidewarehouse { get; set; }
		public int WareHousid { get; set; }
		public ProductDto Product { get; set; }
	}
}


