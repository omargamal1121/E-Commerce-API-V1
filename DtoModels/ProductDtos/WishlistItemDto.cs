using E_Commerce.DtoModels.Shared;

namespace E_Commerce.DtoModels.ProductDtos
{
	public class WishlistItemDto : BaseDto
	{
		public int ProductId { get; set; }
		public string UserId { get; set; } = string.Empty;
		public ProductDto? Product { get; set; }
	}
}
