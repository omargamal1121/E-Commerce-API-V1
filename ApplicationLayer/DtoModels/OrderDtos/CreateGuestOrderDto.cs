using System.ComponentModel.DataAnnotations;

namespace Application.DtoModels.OrderDtos
{
    public class GuestOrderItemDto
    {
        [Required]
        public int ProductId { get; set; }
        [Required]
        public int ProductVariantId { get; set; }
        [Required]
        [Range(1, 100, ErrorMessage = "Quantity must be between 1 and 100")]
        public int Quantity { get; set; }
    }

    public class CreateGuestOrderDto
    {
        [Required(ErrorMessage = "Name is required")]
        public string CustomerName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Phone number is required")]
        [Phone(ErrorMessage = "Invalid phone number")]
        public string PhoneNumber { get; set; } = string.Empty;

      
      
        public string Email { get; set; } = string.Empty;

        public string? Governorate { get; set; }

        [Required(ErrorMessage = "City is required")]
        public string City { get; set; } = string.Empty;

        [Required(ErrorMessage = "Street is required")]
        public string Street { get; set; } = string.Empty;

        public string? Building { get; set; }

     

        [StringLength(500, ErrorMessage = "Notes cannot exceed 500 characters")]
        public string? Notes { get; set; }

        [Required]
        [MinLength(1, ErrorMessage = "Guest order must contain at least one item.")]
        public List<GuestOrderItemDto> Items { get; set; } = new();
    }
}
