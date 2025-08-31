using E_Commerce.Enums;
using E_Commerce.Models;
using E_Commerce.Services.EmailServices;
using E_Commerce.Services.Cache;
using E_Commerce.UOW;
using Hangfire;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using E_Commerce.Services.AdminOperationServices;

namespace E_Commerce.Services.PaymentProvidersServices
{
	public interface IPaymentProvidersServices
	{
		Task<Result<PaymentProviderDto>> AddIfNotExistsAsync(CreatePaymentProviderDto dto, string userId);
		Task<Result<PaymentProviderDto>> CreatePaymentProvider(CreatePaymentProviderDto paymentdto, string userid);
		Task<Result<bool>> UpdateAsync(int id, UpdatePaymentProviderDto dto, string userId);
		Task<Result<bool>> RemovePaymentProviderAsync(int id, string userId);
		Task<Result<PaymentProviderDto>> GetPaymentProviderByIdAsync(int id, bool? isDelete = null);
		Task<Result<List<PaymentProviderDto>>> GetPaymentProvidersAsync(bool? isDelete = null, int page = 1, int pageSize = 10);
	}

	public class PaymentProvidersServices : IPaymentProvidersServices
	{
		private readonly ICacheManager _cacheService;
		private readonly IBackgroundJobClient _backgroundJobClient;
		private readonly IErrorNotificationService _errorNotificationService;
		private readonly IAdminOpreationServices _adminOperationServices;
		private readonly IUnitOfWork _unitOfWork;
		private readonly ILogger<PaymentProvidersServices> _logger;

		public const string PAYMENTPROVIDER_CACHE = "PaymentProviderCache";

		public PaymentProvidersServices(
			IUnitOfWork unitOfWork,
			ILogger<PaymentProvidersServices> logger,
			IBackgroundJobClient backgroundJobClient,
			IErrorNotificationService errorNotificationService,
			IAdminOpreationServices adminOpreationServices,
			ICacheManager cacheManager)
		{
			_unitOfWork = unitOfWork;
			_backgroundJobClient = backgroundJobClient;
			_adminOperationServices = adminOpreationServices;
			_errorNotificationService = errorNotificationService;
			_logger = logger;
			_cacheService = cacheManager;
		}

		public async Task<Result<PaymentProviderDto>> AddIfNotExistsAsync(CreatePaymentProviderDto dto, string userId)
		{
			if (dto == null)
			{
				_logger.LogWarning("AddIfNotExistsAsync called with null DTO by user {UserId}", userId);
				return Result<PaymentProviderDto>.Fail("Payment provider data is required.");
			}

			// Case-insensitive name check
			var existing = await _unitOfWork.Repository<PaymentProvider>().GetAll()
				.AnyAsync(m => m.Name.ToLower() == dto.Name.ToLower() && m.DeletedAt == null);
			
			if (existing)
			{
				_logger.LogWarning("Payment provider with name '{Name}' already exists", dto.Name);
				return Result<PaymentProviderDto>.Fail("Payment provider with this name already exists.");
			}

			return await CreatePaymentProvider(dto, userId);
		}

		public async Task<Result<PaymentProviderDto>> CreatePaymentProvider(CreatePaymentProviderDto paymentdto, string userid)
		{
			_logger.LogInformation("Starting CreatePaymentProvider for user {UserId}", userid);

			if (paymentdto == null)
			{
				_logger.LogWarning("CreatePaymentProvider called with null DTO by user {UserId}", userid);
				return Result<PaymentProviderDto>.Fail("Payment provider data is required.");
			}

			using var transaction = await _unitOfWork.BeginTransactionAsync();

			try
			{
				// Validate unique name within transaction
				var existingName = await _unitOfWork.Repository<PaymentProvider>().GetAll()
					.AnyAsync(m => m.Name.ToLower() == paymentdto.Name.ToLower() && m.DeletedAt == null);

				if (existingName)
				{
					_logger.LogWarning("Payment provider with name '{Name}' already exists", paymentdto.Name);
					await transaction.RollbackAsync();
					return Result<PaymentProviderDto>.Fail("Payment provider with this name already exists.");
				}

				var paymentProvider = new PaymentProvider
				{
					Name = paymentdto.Name,
					ApiEndpoint = paymentdto.ApiEndpoint,
					PublicKey = paymentdto.PublicKey,
					PrivateKey = paymentdto.PrivateKey,
					Hmac = paymentdto.Hmac,
					IframeId = paymentdto.IframeId,
					Provider = paymentdto.PaymentProvider
				};

				_logger.LogInformation("Creating payment provider: {PaymentProvider}", paymentProvider.Name);

				var createdProvider = await _unitOfWork.Repository<PaymentProvider>().CreateAsync(paymentProvider);
				if (createdProvider == null)
				{
					_logger.LogWarning("Failed to create payment provider for user {UserId}", userid);
					await transaction.RollbackAsync();
					return Result<PaymentProviderDto>.Fail("Failed to create payment provider");
				}

				var adminOperationResult = await _adminOperationServices.AddAdminOpreationAsync(
					"Add Payment Provider", Opreations.AddOpreation, userid, createdProvider.Id);

				if (!adminOperationResult.Success)
				{
					_logger.LogWarning("Failed to add admin operation for provider {Id}", createdProvider.Id);
					await transaction.RollbackAsync();
					return Result<PaymentProviderDto>.Fail("Failed to add admin operation", 500);
				}

				await transaction.CommitAsync();

				// Clear cache after successful commit
				_backgroundJobClient.Enqueue(() => _cacheService.RemoveByTagAsync(PAYMENTPROVIDER_CACHE));

				_logger.LogInformation("Payment provider created successfully with ID {Id}", createdProvider.Id);
				return Result<PaymentProviderDto>.Ok(new PaymentProviderDto 
				{ 
					Id = createdProvider.Id, 
					Name = createdProvider.Name,
					CreatedAt = createdProvider.CreatedAt.Value,
					IsDeleted = false
				});
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error occurred while creating payment provider for user {UserId}", userid);

				await transaction.RollbackAsync();

				_backgroundJobClient.Enqueue(() =>
					_errorNotificationService.SendErrorNotificationAsync("Error in CreatePaymentProvider", ex.Message));

				return Result<PaymentProviderDto>.Fail("Internal Server Error", 500);
			}
		}

		public async Task<Result<bool>> RemovePaymentProviderAsync(int id, string userId)
		{
			_logger.LogInformation("Starting RemovePaymentProvider for ID {Id} by user {UserId}", id, userId);

			using var transaction = await _unitOfWork.BeginTransactionAsync();
			try
			{
				// Do not allow removing provider if it has active payment methods
				var hasActiveMethods = await _unitOfWork.Repository<PaymentMethod>()
					.GetAll()
					.AnyAsync(m => m.PaymentProviderId == id && m.IsActive && m.DeletedAt == null);
				if (hasActiveMethods)
				{
					_logger.LogWarning("Cannot remove payment provider {Id} because it has active payment methods", id);
					await transaction.RollbackAsync();
					return Result<bool>.Fail("Cannot remove provider with active payment methods. Please deactivate or reassign them first.", 409);
				}

				var existing = await _unitOfWork.Repository<PaymentProvider>().GetByIdAsync(id);
				if (existing == null || existing.DeletedAt != null)
				{
					_logger.LogWarning("Payment provider with ID {Id} not found or already deleted", id);
					await transaction.RollbackAsync();
					return Result<bool>.Fail($"Payment provider with ID {id} not found");
				}

				_logger.LogInformation("Removing payment provider: {Name}", existing.Name);

				var softDeleted = await _unitOfWork.Repository<PaymentProvider>().SoftDeleteAsync(id);
				if (!softDeleted)
				{
					await transaction.RollbackAsync();
					return Result<bool>.Fail("Failed to delete payment provider", 500);
				}

				var log = await _adminOperationServices.AddAdminOpreationAsync(
					"Remove Payment Provider",
					Opreations.DeleteOpreation,
					userId,
					id);
				if (log == null || !log.Success)
				{
					_logger.LogWarning("Failed to add admin operation for provider removal {Id}", id);
					await transaction.RollbackAsync();
					return Result<bool>.Fail("Failed to add admin operation", 500);
				}

				await transaction.CommitAsync();

				// Clear cache after successful commit
				_backgroundJobClient.Enqueue(() => _cacheService.RemoveByTagAsync(PAYMENTPROVIDER_CACHE));

				return Result<bool>.Ok(true);
			}
			catch (Exception ex)
			{
				await transaction.RollbackAsync();
				_logger.LogError(ex, "Error occurred while removing payment provider for ID {Id} by user {UserId}", id, userId);
				_backgroundJobClient.Enqueue(() =>
					_errorNotificationService.SendErrorNotificationAsync("Error in RemovePaymentProvider", ex.Message));
				return Result<bool>.Fail("Failed to remove payment provider", 500);
			}
		}

		public async Task<Result<PaymentProviderDto>> GetPaymentProviderByIdAsync(int id, bool? isDelete = null)
		{
			try
			{
				string cacheKey = $"paymentprovider_id_{id}_isDelete_{isDelete}";

				var cached = await _cacheService.GetAsync<PaymentProviderDto>(cacheKey);
				if (cached != null)
				{
					_logger.LogInformation("Payment provider with ID {Id} fetched from cache.", id);
					return Result<PaymentProviderDto>.Ok(cached);
				}

				_logger.LogInformation("Querying DB for payment provider with ID {Id}", id);

				var query = _unitOfWork.Repository<PaymentProvider>().GetAll().AsNoTracking()
					.Where(p => p.Id == id);

				if (isDelete.HasValue)
				{
					query = isDelete.Value
						? query.Where(p => p.DeletedAt != null)
						: query.Where(p => p.DeletedAt == null);
				}

				var provider = await query
					.Select(p => new PaymentProviderDto
					{
						Id = p.Id,
						Name = p.Name,
						IsDeleted = p.DeletedAt != null,
						CreatedAt = p.CreatedAt ?? DateTime.MinValue,
						UpdatedAt = p.ModifiedAt
					})
					.FirstOrDefaultAsync();

				if (provider == null)
				{
					_logger.LogWarning("Payment provider with ID {Id} not found.", id);
					return Result<PaymentProviderDto>.Fail("Payment provider not found", 404);
				}

				_backgroundJobClient.Enqueue(() =>
					_cacheService.SetAsync(cacheKey, provider, TimeSpan.FromMinutes(5), new[] { PAYMENTPROVIDER_CACHE }));

				_logger.LogInformation("Payment provider with ID {Id} cached successfully.", id);
				return Result<PaymentProviderDto>.Ok(provider);
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "An error occurred while fetching payment provider with ID {Id}", id);
				_backgroundJobClient.Enqueue(() =>
					_errorNotificationService.SendErrorNotificationAsync("Error in GetPaymentProviderByIdAsync", ex.Message));
				return Result<PaymentProviderDto>.Fail("An unexpected error occurred while retrieving the payment provider.", 500);
			}
		}

		public async Task<Result<List<PaymentProviderDto>>> GetPaymentProvidersAsync(bool? isDelete = null, int page = 1, int pageSize = 10)
		{
			try
			{
				if (page <= 0) page = 1;
				if (pageSize <= 0 || pageSize > 100) pageSize = 10;

				string cacheKey = $"paymentproviders_{isDelete}_page{page}_size{pageSize}";

				var cached = await _cacheService.GetAsync<List<PaymentProviderDto>>(cacheKey);
				if (cached != null)
				{
					_logger.LogInformation("Fetched payment providers from cache with key {CacheKey}", cacheKey);
					return Result<List<PaymentProviderDto>>.Ok(cached);
				}

				var query = _unitOfWork.Repository<PaymentProvider>().GetAll().AsQueryable();

				if (isDelete.HasValue)
				{
					query = isDelete.Value
						? query.Where(p => p.DeletedAt != null)
						: query.Where(p => p.DeletedAt == null);
				}

				var data = await query
					.OrderBy(p => p.Id)
					.Skip((page - 1) * pageSize)
					.Take(pageSize)
					.Select(p => new PaymentProviderDto
					{
						Id = p.Id,
						Name = p.Name,
						IsDeleted = p.DeletedAt != null,
						CreatedAt = p.CreatedAt ?? DateTime.MinValue,
						UpdatedAt = p.ModifiedAt,
					})
					.ToListAsync();

				_backgroundJobClient.Enqueue(() => _cacheService.SetAsync(cacheKey, data, TimeSpan.FromMinutes(5), new[] { PAYMENTPROVIDER_CACHE }));
				_logger.LogInformation("Fetched payment providers from DB and cached result.");

				return Result<List<PaymentProviderDto>>.Ok(data);
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error occurred while getting payment providers.");
				_backgroundJobClient.Enqueue(() => _errorNotificationService.SendErrorNotificationAsync("Error in GetPaymentProvidersAsync", ex.Message));
				return Result<List<PaymentProviderDto>>.Fail("An error occurred while fetching payment providers.");
			}
		}

		public async Task<Result<bool>> UpdateAsync(int id, UpdatePaymentProviderDto dto, string userId)
		{
			if (dto == null)
			{
				_logger.LogWarning("UpdateAsync called with null DTO by user {UserId}", userId);
				return Result<bool>.Fail("Payment provider data is required.");
			}

			using var transaction = await _unitOfWork.BeginTransactionAsync();

			try
			{
				var paymentprovider = await _unitOfWork.Repository<PaymentProvider>().GetByIdAsync(id);
				if (paymentprovider == null || paymentprovider.DeletedAt != null)
				{
					await transaction.RollbackAsync();
					return Result<bool>.Fail("Payment provider not found or deleted");
				}

				// Check for duplicate name (case-insensitive)
				if (paymentprovider.Name.ToLower() != dto.Name.ToLower())
				{
					var nameExists = await _unitOfWork.Repository<PaymentProvider>().GetAll()
						.AnyAsync(p => p.Name.ToLower() == dto.Name.ToLower() && p.Id != id && p.DeletedAt == null);
					
					if (nameExists)
					{
						_logger.LogWarning("Payment provider with name '{Name}' already exists", dto.Name);
						await transaction.RollbackAsync();
						return Result<bool>.Fail("Payment provider with this name already exists.");
					}
				}

				var updatedFields = new List<string>();

				if (paymentprovider.Name != dto.Name)
				{
					updatedFields.Add($"Name changed from '{paymentprovider.Name}' to '{dto.Name}'");
					paymentprovider.Name = dto.Name;
				}

				if (!string.Equals(paymentprovider.ApiEndpoint, dto.ApiEndpoint))
				{
					updatedFields.Add($"ApiEndpoint changed from '{paymentprovider.ApiEndpoint}' to '{dto.ApiEndpoint}'");
					paymentprovider.ApiEndpoint = dto.ApiEndpoint;
				}

				if (!string.Equals(paymentprovider.PublicKey, dto.PublicKey))
				{
					updatedFields.Add($"PublicKey changed");
					paymentprovider.PublicKey = dto.PublicKey;
				}

				if (!string.Equals(paymentprovider.PrivateKey, dto.PrivateKey))
				{
					updatedFields.Add($"PrivateKey changed");
					paymentprovider.PrivateKey = dto.PrivateKey;
				}

				if (!string.Equals(paymentprovider.Hmac, dto.Hmac))
				{
					updatedFields.Add($"Hmac changed");
					paymentprovider.Hmac = dto.Hmac;
				}

				if (!string.Equals(paymentprovider.IframeId, dto.IframeId))
				{
					updatedFields.Add($"IframeId changed from '{paymentprovider.IframeId}' to '{dto.IframeId}'");
					paymentprovider.IframeId = dto.IframeId;
				}

				if (paymentprovider.Provider != dto.PaymentProvider)
				{
					updatedFields.Add($"Provider changed from '{paymentprovider.Provider}' to '{dto.PaymentProvider}'");
					paymentprovider.Provider = dto.PaymentProvider;
				}

				// Only update if there are changes
				if (updatedFields.Count == 0)
				{
					await transaction.RollbackAsync();
					return Result<bool>.Ok(true); // No changes needed
				}

				paymentprovider.ModifiedAt = DateTime.UtcNow;

				var logResult = await _adminOperationServices.AddAdminOpreationAsync(
					string.Join(" | ", updatedFields),
					Opreations.UpdateOpreation,
					userId,
					paymentprovider.Id
				);

				if (logResult == null || !logResult.Success)
				{
					_logger.LogWarning("Failed to add admin operation for provider update {Id}", id);
					await transaction.RollbackAsync();
					return Result<bool>.Fail("Failed to log admin operation.");
				}

				await _unitOfWork.CommitAsync();
				await transaction.CommitAsync();

				// Clear cache after successful commit
				_backgroundJobClient.Enqueue(() => _cacheService.RemoveByTagAsync(PAYMENTPROVIDER_CACHE));

				return Result<bool>.Ok(true);
			}
			catch (Exception ex)
			{
				await transaction.RollbackAsync();
				_logger.LogError(ex, "Error while updating payment provider {Id}", id);
				_backgroundJobClient.Enqueue(() =>
					_errorNotificationService.SendErrorNotificationAsync("Error in UpdateAsync", ex.Message));
				return Result<bool>.Fail("Unexpected error occurred.");
			}
		}
	}
	public class CreatePaymentProviderDto
	{
		[Required(ErrorMessage = "Payment Provider name is required.")]
		[StringLength(50, MinimumLength = 3, ErrorMessage = "Name must be between 3 and 50 characters.")]
		[RegularExpression(@"^[a-zA-Z0-9][a-zA-Z0-9\s\-,]*[a-zA-Z0-9]$", ErrorMessage = "Name must start and end with an alphanumeric character and can contain spaces, hyphens, and commas in between.")]
		public string Name { get; set; } = string.Empty;

		[Required(ErrorMessage = "API Endpoint is required.")]
		[StringLength(100, MinimumLength = 3, ErrorMessage = "API must be between 3 and 100 characters.")]
		public string ApiEndpoint { get; set; } = string.Empty;

		[StringLength(200, ErrorMessage = "Public Key is too long.")]
		public string? PublicKey { get; set; }

		[StringLength(200, ErrorMessage = "Private Key is too long.")]
		public string? PrivateKey { get; set; }
		public string? Hmac { get; set; }
		public PaymentProviderEnums PaymentProvider { get; set; }
		public string? IframeId { get; set; }

	}
	public class UpdatePaymentProviderDto
	{
		[Required(ErrorMessage = "Payment Provider name is required.")]
		[StringLength(50, MinimumLength = 3, ErrorMessage = "Name must be between 3 and 50 characters.")]
		[RegularExpression(@"^[a-zA-Z0-9][a-zA-Z0-9\s\-,]*[a-zA-Z0-9]$", ErrorMessage = "Name must start and end with an alphanumeric character and can contain spaces, hyphens, and commas in between.")]
		public string Name { get; set; } = string.Empty;

		[Required(ErrorMessage = "API Endpoint is required.")]
		[StringLength(100, MinimumLength = 3, ErrorMessage = "API must be between 3 and 100 characters.")]
		public string ApiEndpoint { get; set; } = string.Empty;

		[StringLength(200, ErrorMessage = "Public Key is too long.")]
		public string? PublicKey { get; set; }

		[StringLength(200, ErrorMessage = "Private Key is too long.")]
		public string? PrivateKey { get; set; }
		public string? Hmac { get; set; }
		public PaymentProviderEnums PaymentProvider { get; set; }
		public string? IframeId { get; set; }

	}
	public class PaymentProviderDto
	{
		public int Id { get; set; }
		public string Name { get; set; }          
		public bool IsDeleted { get; set; }   
		public DateTime CreatedAt { get; set; }     
		public DateTime? UpdatedAt { get; set; }   
	}
}
