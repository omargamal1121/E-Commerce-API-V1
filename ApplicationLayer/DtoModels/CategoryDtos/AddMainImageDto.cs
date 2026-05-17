using Microsoft.AspNetCore.Http;
using System.ComponentModel.DataAnnotations;

namespace Application.DtoModels.CategoryDtos
{
	public class AddMainImageDto
	{
		public IFormFile Image { get; set; }
	}
}


