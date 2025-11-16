using Microsoft.AspNetCore.Http;
using System.ComponentModel.DataAnnotations;

namespace ApplicationLayer.DtoModels.CategoryDtos
{
	public class AddImagesDto
	{
		[Required]
		public List<IFormFile> Images { get; set; }
	}
}


