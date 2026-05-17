using Application.DtoModels.Shared;

namespace Application.DtoModels.ProductDtos
{
	public class ReviewDto : BaseDto
	{
		public int Rating { get; set; }
		public string Comment { get; set; } = string.Empty;
		public string UserId { get; set; } = string.Empty;
		public string UserName { get; set; } = string.Empty;
		public int ProductId { get; set; }
	}
}


