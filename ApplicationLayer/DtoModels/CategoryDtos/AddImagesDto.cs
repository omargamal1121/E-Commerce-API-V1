using Microsoft.AspNetCore.Http;
using System.ComponentModel.DataAnnotations;

namespace Application.DtoModels.CategoryDtos
{
	public class AddImagesDto
	{
		[Required]
		public List<IFormFile> Images { get; set; }
	}
}


