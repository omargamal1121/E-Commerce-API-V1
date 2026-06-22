using System.ComponentModel.DataAnnotations;

namespace Application.DtoModels.CartDtos
{
    public class CreateCartItemDto
    {
        [Required(ErrorMessage = "Product ID is required")]
        [Range(1, int.MaxValue, ErrorMessage = "Product ID must be greater than 0")]
        public int ProductId { get; set; }

        [Range(1,50,ErrorMessage = "Quantity Must be between 1 to 50 ")]
        [Required(ErrorMessage = "Quantity is required")]
        public int Quantity { get; set; }

        public int ProductVariantId { get; set; }
    }

    public class UpdateCartItemDto
    {
        [Range(1, 50, ErrorMessage = "Quantity Must be between 1 to 50 ")]
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

