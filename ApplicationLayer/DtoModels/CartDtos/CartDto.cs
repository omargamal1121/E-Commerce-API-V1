
using ApplicationLayer.DtoModels.ProductDtos;
using ApplicationLayer.DtoModels.Shared;

namespace ApplicationLayer.DtoModels.CartDtos
{
    public class CartDto : BaseDto
    {
        public string UserId { get; set; } = string.Empty;
        public IEnumerable<CartItemDto> Items { get; set; }
        public decimal TotalPrice { get; set; }
        public int TotalItems { get; set; }
        public bool IsEmpty =>Items==null?false: !Items.Any();
		public DateTime? CheckoutDate { get; set; }

	}

	public class CartItemDto : BaseDto
    {
        public int ProductId { get; set; }
      
        public required ProductForCartDto Product { get; set; }
        public int Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public DateTime AddedAt { get; set; }
    }

    

  

   
   
   
} 

