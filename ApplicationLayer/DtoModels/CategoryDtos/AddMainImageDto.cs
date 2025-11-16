using Microsoft.AspNetCore.Http;
using System.ComponentModel.DataAnnotations;

namespace ApplicationLayer.DtoModels.CategoryDtos
{
	public class AddMainImageDto
	{
		public IFormFile Image { get; set; }
	}
}


