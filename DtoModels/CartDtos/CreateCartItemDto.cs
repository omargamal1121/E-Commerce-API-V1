using System.ComponentModel.DataAnnotations;

namespace E_Commerce.DtoModels.CartDtos
{
    public class CreateCartItemDto
    {
        [Required(ErrorMessage = "Product ID is required")]
        [Range(1, int.MaxValue, ErrorMessage = "Product ID must be greater than 0")]
        public int ProductId { get; set; }

        [Required(ErrorMessage = "Quantity is required")]
        public int Quantity { get; set; }

        public int ProductVariantId { get; set; }
    }

    public class UpdateCartItemDto
    {
        [Required(ErrorMessage = "Quantity is required")]
        public int Quantity { get; set; }
    }

    public class RemoveCartItemDto
    {
        [Required(ErrorMessage = "Product ID is required")]
        [Range(1, int.MaxValue, ErrorMessage = "Product ID must be greater than 0")]
        public int ProductId { get; set; }

        public int? ProductVariantId { get; set; }
    }
} 