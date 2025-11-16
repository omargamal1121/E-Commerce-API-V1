
using DomainLayer.Enums;
using System.ComponentModel.DataAnnotations;

namespace DomainLayer.Models
{
	public class AdminOperationsLog :BaseEntity
	{
		public string AdminId { get; set; } = string.Empty;
		public   Customer Admin { get; set; }

		public Opreations OperationType { get; set; } = Opreations.AddOpreation;
		public List<int>? ItemId { get; set; }
		[RegularExpression(@"^[\w\s.,\-()'\""]{0,500}$", ErrorMessage = "Description can contain up to 500 characters: letters, numbers, spaces, and .,-()'\"")]
		public string Description { get; set; } = string.Empty;
		public DateTime Timestamp { get; set; } = DateTime.UtcNow;
	}
}
