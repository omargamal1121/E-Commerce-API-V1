using Microsoft.AspNetCore.Http;

namespace ApplicationLayer.DtoModels.AccountDtos
{
	public record UploadPhotoDto(IFormFile image);

}


