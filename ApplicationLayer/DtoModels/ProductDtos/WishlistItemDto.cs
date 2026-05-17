using Application.DtoModels.Shared;

namespace Application.DtoModels.ProductDtos
{
	public class WishlistItemDto : BaseDto
	{
		public int ProductId { get; set; }
		public string UserId { get; set; } = string.Empty;
		public ProductDto? Product { get; set; }
	}
}


