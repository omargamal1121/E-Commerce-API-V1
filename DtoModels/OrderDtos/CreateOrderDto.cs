using E_Commerce.Enums;
using System.ComponentModel.DataAnnotations;

namespace E_Commerce.DtoModels.OrderDtos
{
	public class CreateOrderDto
	{
		[Required]
		public int AddressId { get; set; } 

		[StringLength(500, ErrorMessage = "Notes cannot exceed 500 characters")]
		public string? Notes { get; set; }

	}

	public class UpdateOrderStatusDto
    {
        [Required(ErrorMessage = "Order status is required")]
        public OrderStatus Status { get; set; }

        [StringLength(500, ErrorMessage = "Notes cannot exceed 500 characters")]
        public string? Notes { get; set; }
    }

    public class CancelOrderDto
    {
        [Required(ErrorMessage = "Cancellation reason is required")]
        [StringLength(500, MinimumLength = 10, ErrorMessage = "Cancellation reason must be between 10 and 500 characters")]
        public string CancellationReason { get; set; } = string.Empty;
    }
} 