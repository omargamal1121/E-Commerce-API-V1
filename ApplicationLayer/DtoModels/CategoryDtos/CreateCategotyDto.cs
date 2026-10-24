
using System.ComponentModel.DataAnnotations;

namespace Application.DtoModels.CategoryDtos
{
	public class CreateCategotyDto
	{
		[Required(ErrorMessage = "Name is required.")]
		public string Name { get; set; } = string.Empty;
		[Required(ErrorMessage = "Description is required.")]
		public string Description { get; set; } = string.Empty;

		public int DisplayOrder { get; set; }
	}
}


