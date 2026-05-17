
using Application.DtoModels.ProductDtos;
using Application.DtoModels.Shared;

namespace Application.DtoModels.CartDtos
{
	public class CartDto : BaseDto
	{
		public string UserId { get; set; } = string.Empty;

		public IEnumerable<CartItemDto> Items { get; set; } = new List<CartItemDto>();

		public decimal TotalPriceAtAddTime { get; set; }   
		public decimal TotalCurrentPrice { get; set; }

		public bool HasPriceChanges { get; set; }

		public int TotalItems { get; set; }

		public bool IsEmpty => !Items.Any();

		public DateTime? CheckoutDate { get; set; }
	}
	public class CartItemDto : BaseDto
	{
		public int ProductId { get; set; }

		public required ProductForCartDto Product { get; set; }

		public int Quantity { get; set; }

		public decimal PriceAtAddTime { get; set; }   
		public DateTime AddedAt { get; set; }

		public decimal CurrentPrice { get; set; }

		public bool IsPriceChanged { get; set; }      
	}







} 

