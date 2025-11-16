using Microsoft.VisualBasic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using DomainLayer.Enums;

namespace DomainLayer.Models
{
	public class Product : BaseEntity
	{
		[Required(ErrorMessage = "Name is required.")]
		[StringLength(
		20,
		MinimumLength = 5,
		ErrorMessage = "Name must be between 5 and 20 characters."
	)]
		[RegularExpression(@"^[a-zA-Z0-9][a-zA-Z0-9\s\-,]*[a-zA-Z0-9]$", ErrorMessage = "Name must start and end with an alphanumeric character and can contain spaces, hyphens, and commas in between.")]
		public string Name { get; set; } = string.Empty;

		[Required(ErrorMessage = "Description is required.")]
		[StringLength(
			50,
			MinimumLength = 10,
			ErrorMessage = "Description must be between 10 and 50 characters."
		)]
[RegularExpression(@"^[\w\s.,\-()'\""]{0,500}$", ErrorMessage = "Description can contain up to 500 characters: letters, numbers, spaces, and .,-()'\"")]
		public string Description { get; set; } = string.Empty;


		[ForeignKey("Category")]
		public int SubCategoryId { get; set; }
		public SubCategory SubCategory { get; set; }

		[Required(ErrorMessage = "Quantity Required")]
		[Range(0,int.MaxValue)]
		public int Quantity { get; set; }
		public ICollection<ProductInventory> InventoryEntries { get; set; } = new List<ProductInventory>();

		[ForeignKey("Discount")]
		public int? DiscountId { get; set; } 
		public  Discount? Discount { get; set; }

		public ICollection<Image> Images { get; set; } = new List<Image>();

		[Timestamp]
		[Column(TypeName = "binary(8)")]
		public byte[]? RowVersion { get; set; } = null;

		// Product Variants
		public ICollection<ProductVariant> ProductVariants { get; set; } = new List<ProductVariant>();

		public ICollection<ProductCollection> ProductCollections { get; set; } = new List<ProductCollection>();

		public ICollection<Review> Reviews { get; set; } = new List<Review>();
		public ICollection<CartItem> CartItems { get; set; } = new List<CartItem>();
		public ICollection<OrderItem> OrderItems { get; set; } = new List<OrderItem>();
		
	


		public  decimal Price { get; set; }

		public FitType fitType { get; set; }

		public ICollection<ReturnRequestProduct> ReturnRequestProducts { get; set; } = new List<ReturnRequestProduct>();

		public ICollection<WishlistItem> WishlistItems { get; set; } = new List<WishlistItem>();
		[Range(1,4)]
		public Gender Gender { get; set; }
		public  bool IsActive { get; set; }
	}
	public enum FitType
	{
		Regular,
		Slim,
		Loose,
		Skinny,
		Relaxed,
		Oversized
	}
}
