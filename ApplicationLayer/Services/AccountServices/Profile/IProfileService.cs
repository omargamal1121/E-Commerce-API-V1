using Application.DtoModels.AccountDtos;
using Application.DtoModels.Responses;
using Application.Services;
using Microsoft.AspNetCore.Http;

namespace Application.Services.AccountServices.Profile
{
    public interface IProfileService
    {
        Task<Result<ChangeEmailResultDto>> ChangeEmailAsync(string newEmail, string userid);
        Task<Result<UploadPhotoResponseDto>> UploadPhotoAsync(IFormFile image, string id);
        Task<Result<ProfileDto>> GetProfileAsync(string userId);
    }
}

