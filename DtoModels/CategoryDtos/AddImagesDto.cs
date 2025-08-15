using System.ComponentModel.DataAnnotations;

namespace E_Commerce.DtoModels.CategoryDtos
{
	public class AddImagesDto
	{
		[Required]
		public List<IFormFile> Images { get; set; }
	}
}
