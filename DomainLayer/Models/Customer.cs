using DomainLayer.Enums;
using Microsoft.AspNetCore.Identity;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DomainLayer.Models
{
	public class Customer:IdentityUser
	{
		[Required(ErrorMessage = "Name Required")]
		[RegularExpression(@"^[a-zA-Z0-9][a-zA-Z0-9\s\-,]*[a-zA-Z0-9]$", ErrorMessage = "Name must start and end with an alphanumeric character and can contain spaces, hyphens, and commas in between.")]
		public string Name { get; set; } = string.Empty;

		[Required(ErrorMessage = "Age Required")]
		[Range(18, 100, ErrorMessage = "Must be between 18 and 100")]
		public int Age { get; set; }


		public Gender Gender { get; set; }



		public DateTime CreateAt { get; set; } = DateTime.UtcNow;
		public DateTime? DeletedAt { get; set; }
		public DateTime? LastVisit { get; set; }

		public List<Order> Orders { get; set; } = new List<Order>();
		public ICollection<CustomerAddress> Addresses { get; set; } = new List<CustomerAddress>();
		public ICollection<AdminOperationsLog> adminOperationsLogs{ get; set; } = new List<AdminOperationsLog>();
		public ICollection<UserOperationsLog>  userOperationsLogs { get; set; } = new List<UserOperationsLog>();

		[ForeignKey("Image")]
		public int? ImageId { get; set; }   
		public Image? Image { get; set; }


		public ICollection<Review> Reviews { get; set; } = new List<Review>();

		public ICollection<ReturnRequest> ReturnRequests { get; set; } = new List<ReturnRequest>();

		public ICollection<WishlistItem> WishlistItems { get; set; } = new List<WishlistItem>();
		
	}
}
