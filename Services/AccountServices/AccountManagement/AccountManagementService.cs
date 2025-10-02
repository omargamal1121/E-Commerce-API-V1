using E_Commerce.DtoModels.Responses;
using E_Commerce.Enums;
using E_Commerce.ErrorHnadling;
using E_Commerce.Models;
using E_Commerce.Services.EmailServices;
using E_Commerce.Services.UserOpreationServices;
using E_Commerce.UOW;
using Hangfire;
using Microsoft.AspNetCore.Identity;

namespace E_Commerce.Services.AccountServices.AccountManagement
{
    public class AccountManagementService : IAccountManagementService
    {
        private readonly ILogger<AccountManagementService> _logger;
        private readonly UserManager<Customer> _userManager;
        private readonly IUnitOfWork _unitOfWork;
        private readonly IBackgroundJobClient _backgroundJobClient;
        private readonly IUserOpreationServices _userOpreationServices;
        private readonly IErrorNotificationService _errorNotificationService;

        public AccountManagementService(
            IUserOpreationServices userOpreationServices,
            IBackgroundJobClient backgroundJobClient,
            ILogger<AccountManagementService> logger,
            UserManager<Customer> userManager,
            IUnitOfWork unitOfWork,
            IErrorNotificationService errorNotificationService)
        {
            _logger = logger;
            _userManager = userManager;
            _unitOfWork = unitOfWork;
            _backgroundJobClient = backgroundJobClient;
            _userOpreationServices = userOpreationServices;
            _errorNotificationService = errorNotificationService;
        }

        public async Task<Result<bool>> DeleteAsync(string id)
        {
            _logger.LogInformation("Executing {Method} for UserId: {UserId}", nameof(DeleteAsync), id);

            using var tran = await _unitOfWork.BeginTransactionAsync();
            try
            {
                var customer = await _userManager.FindByIdAsync(id);

                if (customer is null || customer.DeletedAt is not null)
                {
                    _logger.LogWarning("Customer not found or already deleted. UserId: {UserId}", id);
                    return Result<bool>.Fail($"Can't find Customer with this id: {id}", 404);
                }

                customer.DeletedAt = DateTime.UtcNow;

                var result = await _userManager.UpdateAsync(customer);
                if (!result.Succeeded)
                {
                    var errors = string.Join(", ", result.Errors.Select(e => e.Description));
                    _logger.LogError("Failed to soft delete user {UserId}. Errors: {Errors}", customer.Id, errors);

                    await tran.RollbackAsync();
                    return Result<bool>.Fail($"Can't delete account now. Errors: {errors}", 500);
                }
                await _userManager.UpdateSecurityStampAsync(customer);



                await tran.CommitAsync();
                _logger.LogInformation("Soft delete successful for UserId: {UserId}", id);

                return Result<bool>.Ok(true, "Deleted", 200);
            }
            catch (Exception ex)
            {
                await tran.RollbackAsync();
                _logger.LogError(ex, "Exception during {Method} for UserId: {UserId}", nameof(DeleteAsync), id);

                _backgroundJobClient.Enqueue<IErrorNotificationService>(e =>
                    e.SendErrorNotificationAsync(ex.Message, ex.StackTrace));

                return Result<bool>.Fail($"Error: {ex.Message}", 500);
            }
        }

      
    }
}
