using Microsoft.AspNetCore.Http;

namespace Application.DtoModels.AccountDtos
{
	public record UploadPhotoDto(IFormFile image);

}


