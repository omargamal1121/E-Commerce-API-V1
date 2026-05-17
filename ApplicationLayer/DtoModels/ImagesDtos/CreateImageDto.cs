using Microsoft.AspNetCore.Http;
using System.ComponentModel.DataAnnotations;

namespace Application.DtoModels.ImagesDtos
{
	public class CreateImageDto
	{
		[Required]
		public List<IFormFile> Files { get; set; } = new List<IFormFile>();
	}
} 

