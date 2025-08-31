using E_Commerce.DtoModels.AccountDtos;
using E_Commerce.DtoModels.ImagesDtos;
using E_Commerce.DtoModels.Responses;
using E_Commerce.Enums;
using E_Commerce.ErrorHnadling;
using E_Commerce.Interfaces;
using E_Commerce.Models;
using E_Commerce.Services.EmailServices;
using E_Commerce.UOW;
using Hangfire;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;
using Newtonsoft.Json.Linq;
using System.Text;

namespace E_Commerce.Services.AccountServices.Profile
{
    public class ProfileService : IProfileService
    {
        private readonly ILogger<ProfileService> _logger;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly UserManager<Customer> _userManager;
        private readonly IImagesServices _imagesService;
        private readonly ICustomerAddressServices _customerAddressServices;
        private readonly IUnitOfWork _unitOfWork;
        private readonly IErrorNotificationService _errorNotificationService;

        public ProfileService(
            ICustomerAddressServices customerAddressServices,
            IHttpContextAccessor httpContextAccessor,
            ILogger<ProfileService> logger,
            UserManager<Customer> userManager,
            IImagesServices imagesService,
            IUnitOfWork unitOfWork,
            IErrorNotificationService errorNotificationService)
        {

            _customerAddressServices = customerAddressServices; 
            _logger = logger;
            _httpContextAccessor = httpContextAccessor;
            _userManager = userManager;
            _imagesService = imagesService;
            _unitOfWork = unitOfWork;
            _errorNotificationService = errorNotificationService;
        }

        public async Task<Result<ChangeEmailResultDto>> ChangeEmailAsync(
            string newEmail,
            string userid
        )
        {
            _logger.LogInformation($"Executing {nameof(ChangeEmailAsync)} for user ID: {userid}");
            var user = await _userManager.FindByIdAsync(userid);
            if (user == null)
            {
                _logger.LogWarning("Change email failed: User not found.");
                return Result<ChangeEmailResultDto>.Fail("User not found.", 401);
            }
            if (string.IsNullOrWhiteSpace(newEmail))
            {
                _logger.LogWarning("Change email failed: Email parameters cannot be null or empty.");
                return Result<ChangeEmailResultDto>.Fail("Email addresses cannot be empty.", 400);
            }
            if (newEmail.Equals(user.Email))
            {
                _logger.LogWarning("Use same Email");
                return Result<ChangeEmailResultDto>.Fail("Can't Use Same Email Address", 400);
            }
            using var transaction = await _unitOfWork.BeginTransactionAsync();
            try
            {
                var existingEmailUser = await _userManager.FindByEmailAsync(newEmail);
                if (existingEmailUser != null)
                {
                    _logger.LogWarning("Change email failed: New email already exists.");
                    return Result<ChangeEmailResultDto>.Fail("This email already exists and can't be used again.", 409);
                }
                var result = await _userManager.SetEmailAsync(user, newEmail);
                if (!result.Succeeded)
                {
                    var errorMessages = string.Join("; ", result.Errors.Select(e => e.Description));
                    _logger.LogError($"Failed to change email: {errorMessages}");
                    return Result<ChangeEmailResultDto>.Fail($"Email Updating: {errorMessages}", 400);
                }
                await transaction.CommitAsync();
				var token = await _userManager.GenerateEmailConfirmationTokenAsync(user);
				var encodedToken = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(token));

				var request = _httpContextAccessor?.HttpContext?.Request;

				if (request != null)
				{
					var baseUrl = $"{request.Scheme}://{request.Host}";
					BackgroundJob.Enqueue<IAccountEmailService>(e => e.SendValidationEmailAsync(user.Email, user.Id, encodedToken, baseUrl));
				}
			
                _logger.LogInformation("Email changed successfully.");
                return Result<ChangeEmailResultDto>.Ok(new ChangeEmailResultDto
                {
                    NewEmail = newEmail,
                    Note = "please go to email confirm",
                }, "Email changed successfully.", 200);
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError($"Exception in ChangeEmailAsync: {ex}");
                BackgroundJob.Enqueue<IErrorNotificationService>(e =>
                    e.SendErrorNotificationAsync(ex.Message, ex.StackTrace)
                );
                return Result<ChangeEmailResultDto>.Fail("An unexpected error occurred.", 500);
            }
        }

        public async Task<Result<UploadPhotoResponseDto>> UploadPhotoAsync(
            IFormFile image,
            string id
        )
        {
            const string loggerAction = nameof(UploadPhotoAsync);
            _logger.LogInformation("Executing {Action} for user ID: {UserId}", loggerAction, id);
            try
            {
                if (image == null || image.Length == 0)
                {
                    _logger.LogWarning("No image file provided for user ID: {UserId}", id);
                    return Result<UploadPhotoResponseDto>.Fail("No image file provided.", 400);
                }
                var pathResult = await _imagesService.SaveCustomerImageAsync(image, id);
                if (!pathResult.Success || pathResult.Data == null)
                {
                    _logger.LogError(pathResult.Message);
                    return Result<UploadPhotoResponseDto>.Fail("Can't Save Image", 500);
                }
                await using var transaction = await _unitOfWork.BeginTransactionAsync();
                var customer = await _userManager.FindByIdAsync(id);
                if (customer == null)
                {
                    _logger.LogError($"User not found with ID: {id}", id);
                    return Result<UploadPhotoResponseDto>.Fail("Can't Found User with this id", 401);
                }
                await ReplaceCustomerImageAsync(customer, pathResult.Data);
                var updateResult = await _userManager.UpdateAsync(customer);
                if (!updateResult.Succeeded)
                {
                    _logger.LogError("Failed to update user profile photo. Errors: {Errors}", string.Join(", ", updateResult.Errors.Select(e => e.Description)));
                    await transaction.RollbackAsync();
                    return Result<UploadPhotoResponseDto>.Fail("Failed to update profile.", 500);
                }
            
                 await transaction.CommitAsync();
                _logger.LogInformation("Successfully uploaded photo for user ID: {UserId}", id);
                return Result<UploadPhotoResponseDto>.Ok(new UploadPhotoResponseDto { ImageUrl = pathResult.Data.Url }, "Photo uploaded successfully.", 200);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error in {Action} for user ID: {UserId}", loggerAction, id);
                BackgroundJob.Enqueue<IErrorNotificationService>(e =>
                    e.SendErrorNotificationAsync(ex.Message, ex.StackTrace)
                );
                return Result<UploadPhotoResponseDto>.Fail("An unexpected error occurred.  Try Again Later", 500);
            }
        }

        public async Task ReplaceCustomerImageAsync(Customer customer, Image newImage)
        {
            try
            {
                if (customer.Image is not null)
                {
                  _=  _imagesService.DeleteImageAsync(newImage);
                }

                customer.Image = newImage;
                await AddOperationAsync(customer.Id, "Change Photo", Opreations.UpdateOpreation);
                await _unitOfWork.CommitAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error in ReplaceCustomerImageAsync: {ex.Message}");
                BackgroundJob.Enqueue<IErrorNotificationService>(e =>
                    e.SendErrorNotificationAsync(ex.Message, ex.StackTrace)
                );
            }
        }

        private async Task AddOperationAsync(
            string userid,
            string description,
            Opreations opreation
        )
        {
            try
            {
                await _unitOfWork
                    .Repository<UserOperationsLog>()
                    .CreateAsync(
                        new UserOperationsLog
                        {
                            Description = description,
                            OperationType = opreation,
                            UserId = userid,
                            Timestamp = DateTime.UtcNow,
                        }
                    );
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error in AddOperationAsync: {ex.Message}");
                BackgroundJob.Enqueue<IErrorNotificationService>(e =>
                    e.SendErrorNotificationAsync(ex.Message, ex.StackTrace)
                );
            }
        }

		public async Task<Result<ProfileDto>> GetProfileAsync(string userId)
		{
			const string loggerAction = nameof(GetProfileAsync);
			_logger.LogInformation("Executing {Action} for user ID: {UserId}", loggerAction, userId);

			try
			{
				var user = await _userManager.FindByIdAsync(userId);
				if (user == null)
				{
					_logger.LogWarning("Get profile failed: User not found.");
					return Result<ProfileDto>.Fail("User not found.", 404);
				}

				var profile = new ProfileDto
				{
					Name = user.Name,
					Email = user.Email,
					PhoneNumber = user.PhoneNumber,
					IsConfirmed = user.EmailConfirmed,
					Gender = user.Gender.ToString(),
                    UserName =user.UserName,
				};
				if (user.Image != null)
				{
					profile.ProfileImage = new ImageDto
					{
						Id = user.Image.Id,
						Url = user.Image.Url,
					};
				}
                var addresses= await _customerAddressServices.GetCustomerAddressesAsync(userId);
                if (addresses.Success&&addresses.Data!=null)
                    profile.customerAddresses = addresses.Data;

				_logger.LogInformation("Successfully retrieved profile for user ID: {UserId}", userId);
				return Result<ProfileDto>.Ok(profile, "Profile retrieved successfully.", 200);
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Unexpected error in {Action} for user ID: {UserId}", loggerAction, userId);
				BackgroundJob.Enqueue<IErrorNotificationService>(e =>
					e.SendErrorNotificationAsync(ex.Message, ex.StackTrace)
				);
				return Result<ProfileDto>.Fail("An unexpected error occurred while retrieving profile.", 500);
			}
		}
	}
}



