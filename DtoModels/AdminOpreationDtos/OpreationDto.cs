using E_Commerce.Enums;
using E_Commerce.Models;
using System.ComponentModel.DataAnnotations;

namespace E_Commerce.DtoModels.AdminOpreationDtos
{
	public class OpreationDto
	{
		public string Id { get; set; } = string.Empty;
		public string Name { get; set; }

		public string OperationType { get; set; } = Opreations.AddOpreation.ToString();
		public List<int> ItemId { get; set; }
		[RegularExpression(@"^[\w\s.,\-()'\""]{0,500}$", ErrorMessage = "Description can contain up to 500 characters: letters, numbers, spaces, and .,-()'\"")]
		public string Description { get; set; } = string.Empty;
		public DateTime Timestamp { get; set; } = DateTime.UtcNow;
	}
}
