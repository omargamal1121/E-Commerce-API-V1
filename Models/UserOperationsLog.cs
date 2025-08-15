using E_Commerce.Enums;
using System.ComponentModel.DataAnnotations;

namespace E_Commerce.Models
{
	public class UserOperationsLog:BaseEntity
	{
		
		public string UserId { get; set; } = string.Empty;
		public Customer User { get; set; }  

		public Opreations OperationType { get; set; } = Opreations.AddOpreation;
		public int ItemId { get; set; }
[RegularExpression(@"^[\w\s.,\-()'\""]{0,500}$", ErrorMessage = "Description can contain up to 500 characters: letters, numbers, spaces, and .,-()'\"")]
		public string Description { get; set; } = string.Empty;
		public DateTime Timestamp { get; set; } = DateTime.UtcNow;
	}
}
