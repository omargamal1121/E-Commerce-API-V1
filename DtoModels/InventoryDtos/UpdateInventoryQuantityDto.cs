using System.ComponentModel.DataAnnotations;

namespace E_Commerce.DtoModels.InventoryDtos
{
    public class UpdateInventoryQuantityDto
    {
        [Required]
        public int ProductId { get; set; }

        [Required]
        public int WarehouseId { get; set; }

        [Required]
        [Range(0, int.MaxValue, ErrorMessage = "Quantity must be greater than or equal to 0")]
        public int NewQuantity { get; set; }
    }
} 