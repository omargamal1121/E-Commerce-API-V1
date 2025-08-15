using System.ComponentModel.DataAnnotations;

namespace E_Commerce.DtoModels.DiscoutDtos
{
	public class CreateDiscountDto
	{
		[Required(ErrorMessage = "Discount name is required")]
		[StringLength(100, ErrorMessage = "Discount name cannot exceed 100 characters")]
		public string Name { get; set; }

		[Required(ErrorMessage = "Discount percentage is required")]
		[Range(1, 100, ErrorMessage = "Discount percentage must be between 1 and 100")]
		public decimal DiscountPercent { get; set; }


		[Required(ErrorMessage = "Start date is required")]
		public DateTime StartDate { get; set; }

		[Required(ErrorMessage = "End date is required")]
		public DateTime EndDate { get; set; }

		public string? Description { get; set; }
	}
}
