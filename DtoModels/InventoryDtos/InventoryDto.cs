using E_Commerce.DtoModels.ProductDtos;
using E_Commerce.DtoModels.Shared;
using E_Commerce.Models;

namespace E_Commerce.DtoModels.InventoryDtos
{
	public class InventoryDto:BaseDto
	{
		public int Quantityinsidewarehouse { get; set; }
		public int WareHousid { get; set; }
		public ProductDto Product { get; set; }
	}
}
