using E_Commerce.DtoModels.AccountDtos;
using E_Commerce.DtoModels.Responses;

namespace E_Commerce.Services.AccountServices.Profile
{
    public interface IProfileService
    {
        Task<Result<ChangeEmailResultDto>> ChangeEmailAsync(string newEmail, string userid);
        Task<Result<UploadPhotoResponseDto>> UploadPhotoAsync(IFormFile image, string id);
        Task<Result<ProfileDto>> GetProfileAsync(string userId);
    }
}