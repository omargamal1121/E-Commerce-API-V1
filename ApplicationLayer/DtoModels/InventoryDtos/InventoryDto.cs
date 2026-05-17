using Application.DtoModels.ProductDtos;
using Application.DtoModels.Shared;

namespace Application.DtoModels.InventoryDtos
{
	public class InventoryDto:BaseDto
	{
		public int Quantityinsidewarehouse { get; set; }
		public int WareHousid { get; set; }
		public ProductDto Product { get; set; }
	}
}


