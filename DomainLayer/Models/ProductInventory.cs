using System.ComponentModel.DataAnnotations;

namespace DomainLayer.Models
{
	public class ProductInventory : BaseEntity
	{
		[Required]
		public int ProductId { get; set; }
		public  Product Product { get; set; }
		[Required]
		public int WarehouseId { get; set; }
		public  Warehouse Warehouse { get; set; }
		[Range(0, int.MaxValue)]
		[Required(ErrorMessage = "Quantity Required")]
		public int Quantity { get; set; }
	}

}
