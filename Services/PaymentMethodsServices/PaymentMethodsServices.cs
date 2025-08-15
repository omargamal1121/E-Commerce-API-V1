using E_Commerce.DtoModels.Responses;
using E_Commerce.Enums;
using E_Commerce.Models;
using E_Commerce.Services.AdminOpreationServices;
using E_Commerce.Services.Cache;
using E_Commerce.Services.EmailServices;
using E_Commerce.UOW;
using Hangfire;
using Microsoft.EntityFrameworkCore;
using static System.Runtime.InteropServices.JavaScript.JSType;
using PaymentMethod = E_Commerce.Models.PaymentMethod;

namespace E_Commerce.Services.PaymentMethodsServices
{
	public interface IPaymentMethodsServices
	{
		public  Task<Result<int?>> GetPaymentMethodIdByEnum(PaymentMethodEnums methodEnum);
		public  Task<Result<bool>> RemovePaymentMethod(int id, string userid);
		public  Task<Result<PaymentMethodDto>> CreatePaymentMethod(Createpaymentmethoddto paymentdto, string userid);
		public  Task<Result<bool>> UpdatePaymentMethod(int id, Updatepaymentmethoddto paymentdto, string userid);
		public  Task<Result<List<PaymentMethodDto>>> GetPaymentMethodsAsync(bool? isActive = null, bool? isDelete = null, int page = 1, int pageSize = 10);
		public  Task<Result<bool>> DeactivatePaymentMethodAsync(int id, string userId);
		public  Task<Result<PaymentMethodDto>> AddIfNotExistsAsync(Createpaymentmethoddto dto, string userId);
		public  Task<Result<bool>> ActivatePaymentMethodAsync(int id, string userId);
		public  Task<Result<PaymentMethodDto>> GetPaymentMethodByIdAsync(int id, bool? isActive = null, bool? isDelete = null);
	}
	public class PaymentMethodsServices: IPaymentMethodsServices
	{
		private readonly ICacheManager _cacheService;
		private readonly IBackgroundJobClient _backgroundJobClient;
		private readonly IErrorNotificationService _errorNotificationService;
		private readonly IAdminOpreationServices _adminOperationServices;
		public readonly IUnitOfWork _unitOfWork;
		public readonly ILogger<PaymentMethodsServices> _logger;
		public const string PAYMENTMETHODCACGE = "Paymentmethodcache";
		public PaymentMethodsServices(ICacheManager cacheManager ,IUnitOfWork unitOfWork,ILogger<PaymentMethodsServices> logger,IBackgroundJobClient backgroundJobClient, IErrorNotificationService errorNotificationService,IAdminOpreationServices adminOpreationServices)
		{
			_cacheService = cacheManager;
			_unitOfWork = unitOfWork;
			_backgroundJobClient = backgroundJobClient;
			_adminOperationServices = adminOpreationServices;
			_errorNotificationService = errorNotificationService;
			_logger = logger;
			
			
		}
		public async Task<Result<PaymentMethodDto>> AddIfNotExistsAsync(Createpaymentmethoddto dto, string userId)
		{
			var existing = await  _unitOfWork.Repository<PaymentMethod>().GetAll()
										 .AnyAsync(m => m.Name == dto.Name && m.PaymentProviderId == dto.PaymentProviderid);
			if (existing)
				return Result<PaymentMethodDto>.Fail("Is Already exsist");

			return await CreatePaymentMethod(dto, userId);
		}

		public async Task<Result<PaymentMethodDto>> CreatePaymentMethod(Createpaymentmethoddto paymentdto, string userid)
		{
			_logger.LogInformation("Starting CreatePaymentMethod for user {UserId}", userid);

			try
			{
				using var transaction = await _unitOfWork.BeginTransactionAsync();
				if (paymentdto == null)
				{
					_logger.LogWarning("CreatePaymentMethod called with null DTO by user {UserId}", userid);
					return Result<PaymentMethodDto>.Fail($"CreatePaymentMethod called with null DTO by user { userid}");
				}

				var paymentmethod = new PaymentMethod
				{
					Name = paymentdto.Name,
					Method = paymentdto.paymentMethod,
					PaymentProviderId =paymentdto.PaymentProviderid,
					IsActive=paymentdto.IsActive,
					IntegrationId =paymentdto.Integrationid
				};

				_logger.LogInformation("Creating payment method: {PaymentMethod}", paymentmethod.Name);

				var aftercreating = await _unitOfWork.Repository<PaymentMethod>().CreateAsync(paymentmethod);
				await _unitOfWork.CommitAsync();

				_logger.LogInformation("Payment method created successfully with ID {Id}", aftercreating.Id);

			 var result=	await _adminOperationServices.AddAdminOpreationAsync("Add Payment Method", Opreations.AddOpreation, userid, aftercreating.Id);
				if (!result.Success)
				{
					await transaction.RollbackAsync();
					return Result<PaymentMethodDto>.Fail("Failed to add admin operation",500);
				}

				_logger.LogInformation("Admin operation logged for payment method {Id}", aftercreating.Id);
				await _unitOfWork.CommitAsync();
				await transaction.CommitAsync();
				_backgroundJobClient.Enqueue(() => _cacheService.RemoveByTagAsync(PAYMENTMETHODCACGE));
				return Result<PaymentMethodDto>.Ok(new PaymentMethodDto { Name =aftercreating.Name,
				Id=aftercreating.Id,
				IsActive= aftercreating.IsActive,
				PaymentProviderid =aftercreating.PaymentProviderId,
				CreatedAt = aftercreating.CreatedAt,
				});
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error occurred while creating payment method for user {UserId}", userid);

				_backgroundJobClient.Enqueue(() =>
					_errorNotificationService.SendErrorNotificationAsync("Error in CreatePaymentMethod", ex.Message));

				return Result<PaymentMethodDto>.Fail("Failed to add admin operation", 500);
			}
		}

		public async Task<Result<bool>> UpdatePaymentMethod(int id, Updatepaymentmethoddto paymentdto, string userid)
		{
			_logger.LogInformation("Starting UpdatePaymentMethod for ID {Id} by user {UserId}", id, userid);

			try
			{
				using var transaction = await _unitOfWork.BeginTransactionAsync();
				
				if (paymentdto == null)
				{
					_logger.LogWarning("UpdatePaymentMethod called with null DTO by user {UserId}", userid);
					return Result<bool>.Fail($"UpdatePaymentMethod called with null DTO by user {userid}");
				}

				var existingPaymentMethod = await _unitOfWork.Repository<PaymentMethod>().GetByIdAsync(id);
				if (existingPaymentMethod == null)
				{
					_logger.LogWarning("Payment method with ID {Id} not found", id);
					return Result<bool>.Fail($"Payment method with ID {id} not found");
				}

				existingPaymentMethod.Name = paymentdto.Name;
				existingPaymentMethod.Method = paymentdto.Method;
				existingPaymentMethod.PaymentProviderId = paymentdto.PaymentProviderid;
				existingPaymentMethod.IntegrationId= paymentdto.Integrationid;


				_logger.LogInformation("Updating payment method: {PaymentMethod}", existingPaymentMethod.Name);

				await _unitOfWork.CommitAsync();

				_logger.LogInformation("Payment method updated successfully with ID {Id}", id);

				var result = await _adminOperationServices.AddAdminOpreationAsync("Update Payment Method", Opreations.UpdateOpreation, userid, id);
				if (!result.Success)
				{
					await transaction.RollbackAsync();
					return Result<bool>.Fail("Failed to add admin operation", 500);
				}

				_logger.LogInformation("Admin operation logged for payment method update {Id}", id);
				await _unitOfWork.CommitAsync();
				await transaction.CommitAsync();

				_backgroundJobClient.Enqueue(() => _cacheService.RemoveByTagAsync(PAYMENTMETHODCACGE));
				return Result<bool>.Ok(true);
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error occurred while updating payment method for ID {Id} by user {UserId}", id, userid);

				_backgroundJobClient.Enqueue(() =>
					_errorNotificationService.SendErrorNotificationAsync("Error in UpdatePaymentMethod", ex.Message));

				return Result<bool>.Fail("Failed to update payment method", 500);
			}
		}

		public async Task<Result<bool>> RemovePaymentMethod(int id, string userid)
		{
			_logger.LogInformation("Starting RemovePaymentMethod for ID {Id} by user {UserId}", id, userid);

			try
			{
				using var transaction = await _unitOfWork.BeginTransactionAsync();

				var existingPaymentMethod = await _unitOfWork.Repository<PaymentMethod>().GetByIdAsync(id);
				if (existingPaymentMethod == null||existingPaymentMethod.DeletedAt!=null)
				{
					_logger.LogWarning("Payment method with ID {Id} not found", id);
					return Result<bool>.Fail($"Payment method with ID {id} not found");
				}

				_logger.LogInformation("Removing payment method: {PaymentMethod}", existingPaymentMethod.Name);

				await _unitOfWork.Repository<PaymentMethod>().SoftDeleteAsync(id);
				await _unitOfWork.CommitAsync();

				_logger.LogInformation("Payment method removed successfully with ID {Id}", id);

				var result = await _adminOperationServices.AddAdminOpreationAsync("Remove Payment Method", Opreations.DeleteOpreation, userid, id);
				if (!result.Success)
				{
					await transaction.RollbackAsync();
					return Result<bool>.Fail("Failed to add admin operation", 500);
				}

				_logger.LogInformation("Admin operation logged for payment method removal {Id}", id);

				_backgroundJobClient.Enqueue(() => _cacheService.RemoveByTagAsync(PAYMENTMETHODCACGE));
				return Result<bool>.Ok(true);
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error occurred while removing payment method for ID {Id} by user {UserId}", id, userid);

				_backgroundJobClient.Enqueue(() =>
					_errorNotificationService.SendErrorNotificationAsync("Error in RemovePaymentMethod", ex.Message));

				return Result<bool>.Fail("Failed to remove payment method", 500);
			}
		}
		public async Task<Result<PaymentMethodDto>> GetPaymentMethodByIdAsync(int id, bool? isActive = null, bool? isDelete = null)
		{
			try
			{
				string cacheKey = $"paymentmethod_id_{id}_isActive_{isActive}_isDelete_{isDelete}";

				var cachedData = await _cacheService.GetAsync<PaymentMethodDto>(cacheKey);
				if (cachedData != null)
				{
					_logger.LogInformation("Payment method with ID {Id} fetched from cache.", id);
					return Result<PaymentMethodDto>.Ok(cachedData);
				}

				_logger.LogInformation("Querying DB for payment method with ID {Id}", id);

				var query = _unitOfWork.Repository<PaymentMethod>().GetAll().AsNoTracking()
					.Where(p => p.Id == id);

				if (isActive.HasValue)
					query = query.Where(p => p.IsActive == isActive.Value);

				if (isDelete.HasValue)
					query = isDelete.Value
						? query.Where(p => p.DeletedAt != null)
						: query.Where(p => p.DeletedAt == null);

				var paymentMethod = await query
					.Select(p => new PaymentMethodDto
					{
						Id = p.Id,
						Name = p.Name,
						PaymentProviderid = p.PaymentProviderId,
						IsActive = p.IsActive,
						paymentMethod = p.Method.ToString()
					})
					.FirstOrDefaultAsync();

				if (paymentMethod == null)
				{
					_logger.LogWarning("Payment method with ID {Id} not found.", id);
					return Result<PaymentMethodDto>.Fail("Payment method not found", 404);
				}

				// Set cache in background
				_backgroundJobClient.Enqueue(() =>
					_cacheService.SetAsync(cacheKey, paymentMethod, TimeSpan.FromMinutes(5), new[] { PAYMENTMETHODCACGE }));

				_logger.LogInformation("Payment method with ID {Id} cached successfully.", id);

				return Result<PaymentMethodDto>.Ok(paymentMethod);
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "An error occurred while fetching payment method with ID {Id}", id);
				_backgroundJobClient.Enqueue(() =>
					_errorNotificationService.SendErrorNotificationAsync("Error in GetPaymentMethodByIdAsync", ex.Message));

				return Result<PaymentMethodDto>.Fail("An unexpected error occurred while retrieving the payment method.", 500);
			}
		}
		public async Task<Result<bool>> ActivatePaymentMethodAsync(int id, string userId)
		{
			using var transaction = await _unitOfWork.BeginTransactionAsync();

			try
			{
				var method = await _unitOfWork.Repository<PaymentMethod>()
					.GetByIdAsync(id);

				if (method == null||method.DeletedAt!=null)
					return Result<bool>.Fail("Payment method not found");

				if (method.IsActive)
					return Result<bool>.Fail("Payment method is already active");

				method.IsActive = true;

				var logResult = await _adminOperationServices.AddAdminOpreationAsync("Activated payment method",Opreations.UpdateOpreation, userId,id);

				if (logResult==null||!logResult.Success)
				{
					await transaction.RollbackAsync();
					return Result<bool>.Fail("Failed to log admin operation");
				}

				await _unitOfWork.CommitAsync();
				await transaction.CommitAsync();

				_backgroundJobClient.Enqueue(() => _cacheService.RemoveByTagAsync(PAYMENTMETHODCACGE));

				return Result<bool>.Ok(true);
			}
			catch (Exception ex)
			{
				await transaction.RollbackAsync();
				_logger.LogError(ex, "Error while activating payment method with id: {Id}", id);
				_backgroundJobClient.Enqueue(() => _errorNotificationService.SendErrorNotificationAsync("Error activating payment method", ex.StackTrace));
				return Result<bool>.Fail("An error occurred");
			}
		}
		public async Task<Result<bool>> DeactivatePaymentMethodAsync(int id, string userId)
		{
			using var transaction = await _unitOfWork.BeginTransactionAsync();

			try
			{
				var method = await _unitOfWork.Repository<PaymentMethod>()
					.GetByIdAsync(id);


				if (method == null)
					return Result<bool>.Fail("Payment method not found");

				if (!method.IsActive)
					return Result<bool>.Fail("Payment method is already inactive");

				method.IsActive = false;

				var logResult = await _adminOperationServices.AddAdminOpreationAsync("Deactivated payment method", Opreations.UpdateOpreation,userId,id);

				if (logResult==null||!logResult.Success)
				{
					await transaction.RollbackAsync();
					return Result<bool>.Fail("Failed to log admin operation");
				}

				await _unitOfWork.CommitAsync();
				await transaction.CommitAsync();

				_backgroundJobClient.Enqueue(() => _cacheService.RemoveByTagAsync(PAYMENTMETHODCACGE));

				return Result<bool>.Ok(true);
			}
			catch (Exception ex)
			{
				await transaction.RollbackAsync();
				_logger.LogError(ex, "Error while deactivating payment method with id: {Id}", id);
				_backgroundJobClient.Enqueue(() => _errorNotificationService.SendErrorNotificationAsync("Error deactivating payment method", ex.StackTrace));
				return Result<bool>.Fail("An error occurred");
			}
		}


		public async Task<Result<List<PaymentMethodDto>>> GetPaymentMethodsAsync(bool? isActive = null, bool? isDelete = null, int page = 1, int pageSize = 10)
		{
			try
			{
				if (page <= 0) page = 1;
				if (pageSize <= 0 || pageSize > 100) pageSize = 10;
				string cacheKey = $"paymentmethods_{isActive}_{isDelete}_page{page}_size{pageSize}";

				// Try to get from cache
				var cachedData = await _cacheService.GetAsync<List<PaymentMethodDto>>(cacheKey);
				if (cachedData != null)
				{
					_logger.LogInformation($"Fetched payment methods from cache with key {cacheKey}");
					return Result<List<PaymentMethodDto>>.Ok(cachedData);
				}

				var query = _unitOfWork.Repository<PaymentMethod>().GetAll().AsQueryable();

				if (isActive.HasValue)
					query = query.Where(p => p.IsActive == isActive.Value);

				if (isDelete.HasValue){
					if(isDelete.Value)
					query = query.Where(p => p.DeletedAt != null);
					else
						query = query.Where(p => p.DeletedAt == null);
				}

				var total = await query.CountAsync();

				var data = await query
					.OrderBy(p => p.Id)
					.Skip((page - 1) * pageSize)
					.Take(pageSize)
					.Select(p => new PaymentMethodDto
					{
						Id = p.Id,
						Name = p.Name,
						IsActive = p.IsActive,
						PaymentProviderid= p.PaymentProviderId,
						paymentMethod = p.Method.ToString()
						
					})
					.ToListAsync();



				_backgroundJobClient.Enqueue(() => _cacheService.SetAsync(cacheKey, data, TimeSpan.FromMinutes(5),new string[]{ PAYMENTMETHODCACGE }));
				_logger.LogInformation("Fetched payment methods from DB and cached result.");

				return Result<List<PaymentMethodDto>>.Ok(data);
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error occurred while getting payment methods.");
				_backgroundJobClient.Enqueue(()=> _errorNotificationService.SendErrorNotificationAsync("Error in GetPaymentMethodAsync", ex.Message));
				return Result<List<PaymentMethodDto>>.Fail("An error occurred while fetching payment methods.");
			}
		}

        public async Task<Result<int?>> GetPaymentMethodIdByEnum(PaymentMethodEnums methodEnum)
		{
			_logger.LogInformation("Getting payment method ID for enum {MethodEnum}", methodEnum);

			try
			{
                var paymentMethod = await _unitOfWork.Repository<PaymentMethod>()
					.GetAll()
                    .Where(p => p.Method == methodEnum && p.IsActive && p.DeletedAt == null)
                    .Select(p => p.Id)
					.FirstOrDefaultAsync();

				if (paymentMethod == 0)
				{
					_logger.LogWarning("Payment method not found for enum {MethodEnum}", methodEnum);
					return Result<int?>.Fail($"Payment method not found for {methodEnum}", 404);
				}

				_logger.LogInformation("Found payment method ID {Id} for enum {MethodEnum}", paymentMethod, methodEnum);
				return Result<int?>.Ok(paymentMethod);
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error getting payment method ID for enum {MethodEnum}", methodEnum);
				_backgroundJobClient.Enqueue(() =>
					_errorNotificationService.SendErrorNotificationAsync("Error getting payment method ID", ex.Message));
				return Result<int?>.Fail("Error getting payment method ID", 500);
			}
		}

	}
	public class Createpaymentmethoddto 
	{
		public string Name { get; set; }
		public  PaymentMethodEnums  paymentMethod { get; set; }
		public int PaymentProviderid { get; set; }
		public bool IsActive { get; set; }
		public string Integrationid { get; set; }
	}
	public class PaymentMethodDto:BaseEntity
	{
		public string Name { get; set; }
		public bool IsActive { get; set; }
		public string paymentMethod { get; set; }
		public int PaymentProviderid { get; set; }

	}
	public class Updatepaymentmethoddto
	{
		public string Name { get; set; }
		public PaymentMethodEnums Method { get; set; }
		public int PaymentProviderid { get; set; }

		public string Integrationid { get; set; }
	}
}



