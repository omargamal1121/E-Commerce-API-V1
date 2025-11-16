using ApplicationLayer.DtoModels.AccountDtos;
using ApplicationLayer.DtoModels.Responses;
using ApplicationLayer.Services;
using Microsoft.AspNetCore.Http;

namespace ApplicationLayer.Services.AccountServices.Profile
{
    public interface IProfileService
    {
        Task<Result<ChangeEmailResultDto>> ChangeEmailAsync(string newEmail, string userid);
        Task<Result<UploadPhotoResponseDto>> UploadPhotoAsync(IFormFile image, string id);
        Task<Result<ProfileDto>> GetProfileAsync(string userId);
    }
}

