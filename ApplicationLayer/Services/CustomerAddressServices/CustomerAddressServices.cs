
using DomainLayer.Enums;
using ApplicationLayer.DtoModels.CustomerAddressDtos;
using ApplicationLayer.DtoModels.Responses;
using ApplicationLayer.ErrorHnadling;
using ApplicationLayer.Interfaces;
using DomainLayer.Models;
using ApplicationLayer.Services.AdminOperationServices;
using ApplicationLayer.Services.Cache;
using ApplicationLayer.Services.EmailServices;

using Hangfire;

using Microsoft.Extensions.Logging;

using System.Linq;

namespace ApplicationLayer.Services.CustomerAddressServices
{
	public class CustomerAddressServices : ICustomerAddressServices
	{
		private readonly ILogger<CustomerAddressServices> _logger;
		private readonly IUnitOfWork _unitOfWork;
		private readonly ICustomerAddressRepository _addressRepository;
		private readonly IAdminOpreationServices _adminOperationServices;
		private readonly ICacheManager _cacheManager;
		private readonly IErrorNotificationService _errorNotificationService;
		private readonly ICustomerAddressMapper _addressMapper;
		private const string CACHE_TAG_ADDRESS = "customer_address";

		public CustomerAddressServices(
			ILogger<CustomerAddressServices> logger,
	
			IUnitOfWork unitOfWork,
			ICustomerAddressRepository addressRepository,
			IAdminOpreationServices adminOperationServices,
			ICacheManager cacheManager,
			IErrorNotificationService errorNotificationService,
			ICustomerAddressMapper addressMapper)
		{
			_logger = logger;

			_unitOfWork = unitOfWork;
			_addressRepository = addressRepository;
			_adminOperationServices = adminOperationServices;
			_cacheManager = cacheManager;
			_errorNotificationService = errorNotificationService;
			_addressMapper = addressMapper;
		}

		private void NotifyAdminOfError(string message, string? stackTrace = null)
		{
			BackgroundJob.Enqueue<IErrorNotificationService>(_ => _.SendErrorNotificationAsync(message, stackTrace));
		}

		public async Task<Result<CustomerAddressDto>> GetAddressByIdAsync(int addressId, string userId)
		{
			_logger.LogInformation($"Getting address by ID: {addressId} for user: {userId}");

			try
			{
				string cacheKey = $"address_{addressId}_{userId}";
				var cachedData = await _cacheManager.GetAsync<CustomerAddressDto>(cacheKey);
				if (cachedData != null)
				{
					_logger.LogInformation("Address retrieved from cache");
					return Result<CustomerAddressDto>.Ok(cachedData, "Address retrieved from cache", 200);
				}

				var address = await _addressRepository.GetAddressByIdAsync(addressId);
				if (address == null)
				{
					_logger.LogWarning($"Address {addressId} not found");
					return Result<CustomerAddressDto>.Fail($"Address with ID {addressId} not found", 404);
				}

				// Check if user owns this address
				if (address.CustomerId != userId)
				{
					_logger.LogWarning($"User {userId} attempted to access address {addressId} owned by {address.CustomerId}");
					return Result<CustomerAddressDto>.Fail("Unauthorized access to address", 403);
				}

				var addressDto = _addressMapper.ToDto(address);
				await _cacheManager.SetAsync(cacheKey, addressDto, tags: new string[] { CACHE_TAG_ADDRESS });

				return Result<CustomerAddressDto>.Ok(addressDto, "Address retrieved successfully", 200);
			}
			catch (Exception ex)
			{
				_logger.LogError($"Error getting address {addressId}: {ex.Message}");
				NotifyAdminOfError($"Error getting address {addressId}: {ex.Message}", ex.StackTrace);
				return Result<CustomerAddressDto>.Fail("An error occurred while retrieving address", 500);
			}
		}

		public async Task<Result<List<CustomerAddressDto>>> GetCustomerAddressesAsync(string userId)
		{
			_logger.LogInformation($"Getting addresses for customer: {userId}");

			try
			{
				string cacheKey = $"customer_addresses_{userId}";
				var cachedData = await _cacheManager.GetAsync<List<CustomerAddressDto>>(cacheKey);
				if (cachedData != null)
				{
					_logger.LogInformation("Customer addresses retrieved from cache");
					return Result<List<CustomerAddressDto>>.Ok(cachedData, "Addresses retrieved from cache", 200);
				}

				var addresses = await _addressRepository.GetAddressesByCustomerAsync(userId);
				var addressDtos = _addressMapper.ToDtos(addresses);
				await _cacheManager.SetAsync(cacheKey, addressDtos, tags: new string[] { CACHE_TAG_ADDRESS });

				return Result<List<CustomerAddressDto>>.Ok(addressDtos, $"Retrieved {addressDtos.Count} addresses", 200);
			}
			catch (Exception ex)
			{
				_logger.LogError($"Error getting addresses for user {userId}: {ex.Message}");
				NotifyAdminOfError($"Error getting addresses for user {userId}: {ex.Message}", ex.StackTrace);
				return Result<List<CustomerAddressDto>>.Fail("An error occurred while retrieving addresses", 500);
			}
		}

		public async Task<Result<CustomerAddressDto>> GetDefaultAddressAsync(string userId)
		{
			_logger.LogInformation($"Getting default address for customer: {userId}");

			try
			{
				string cacheKey = $"default_address_{userId}";
				var cachedData = await _cacheManager.GetAsync<CustomerAddressDto>(cacheKey);
				if (cachedData != null)
				{
					_logger.LogInformation("Default address retrieved from cache");
					return Result<CustomerAddressDto>.Ok(cachedData, "Default address retrieved from cache", 200);
				}

				var address = await _addressRepository.GetDefaultAddressAsync(userId);
				if (address == null)
				{
					_logger.LogWarning($"No default address found for user {userId}");
					return Result<CustomerAddressDto>.Fail("No default address found", 404);
				}

				var addressDto = _addressMapper.ToDto(address);
				await _cacheManager.SetAsync(cacheKey, addressDto, tags: new string[] { CACHE_TAG_ADDRESS });

				return Result<CustomerAddressDto>.Ok(addressDto, "Default address retrieved successfully", 200);
			}
			catch (Exception ex)
			{
				_logger.LogError($"Error getting default address for user {userId}: {ex.Message}");
				NotifyAdminOfError($"Error getting default address for user {userId}: {ex.Message}", ex.StackTrace);
				return Result<CustomerAddressDto>.Fail("An error occurred while retrieving default address", 500);
			}
		}

		public async Task<Result<CustomerAddressDto>> CreateAddressAsync(CreateCustomerAddressDto addressDto, string userId)
		{
			_logger.LogInformation($"Creating new address for user: {userId}");

			using var transaction = await _unitOfWork.BeginTransactionAsync();
			try
			{
				// Check if this is the first address (make it default)
				var addressCount = await _addressRepository.GetAddressCountByCustomerAsync(userId);
				if (addressCount == 0)
				{
					addressDto.IsDefault = true;
				}

				var address = _addressMapper.ToEntity(addressDto, userId);

				var createdAddress = await _addressRepository.CreateAsync(address);
				if (createdAddress == null)
				{
					await transaction.RollbackAsync();
					return Result<CustomerAddressDto>.Fail("Failed to create address", 500);
				}

			
				if (addressDto.IsDefault)
				{
					await _addressRepository.RemoveDefaultFromOtherAddressesAsync(userId, createdAddress.Id);
				}

				var adminLog = await _adminOperationServices.AddAdminOpreationAsync(
					$"Created new address for customer {userId}",
					Opreations.AddOpreation,
					userId,
					createdAddress.Id
				);

				if (!adminLog.Success)
				{
					_logger.LogWarning($"Failed to log admin operation: {adminLog.Message}");
				}

				await _unitOfWork.CommitAsync();
				await transaction.CommitAsync();

				await _cacheManager.RemoveByTagAsync(CACHE_TAG_ADDRESS);

				var resultDto = _addressMapper.ToDto(createdAddress);
				return Result<CustomerAddressDto>.Ok(resultDto, "Address created successfully", 201);
			}
			catch (Exception ex)
			{
				await transaction.RollbackAsync();
				_logger.LogError($"Error creating address for user {userId}: {ex.Message}");
				NotifyAdminOfError($"Error creating address for user {userId}: {ex.Message}", ex.StackTrace);
				return Result<CustomerAddressDto>.Fail("An error occurred while creating address", 500);
			}
		}

		public async Task<Result<CustomerAddressDto>> UpdateAddressAsync(int addressId, UpdateCustomerAddressDto addressDto, string userId)
		{
			_logger.LogInformation($"Updating address {addressId} for user: {userId}");

			using var transaction = await _unitOfWork.BeginTransactionAsync();
			try
			{
				var existingAddress = await _addressRepository.GetAddressByIdAsync(addressId);
				if (existingAddress == null)
				{
					await transaction.RollbackAsync();
					return Result<CustomerAddressDto>.Fail($"Address with ID {addressId} not found", 404);
				}

				// Check if user owns this address
				if (existingAddress.CustomerId != userId)
				{
					await transaction.RollbackAsync();
					return Result<CustomerAddressDto>.Fail("Unauthorized access to address", 403);
				}

				_addressMapper.ApplyUpdates(existingAddress, addressDto);

				// Handle default address setting
				if (addressDto.IsDefault.HasValue && addressDto.IsDefault.Value)
				{
					await _addressRepository.RemoveDefaultFromOtherAddressesAsync(userId, addressId);
					existingAddress.IsDefault = true;
				}

				existingAddress.ModifiedAt = DateTime.UtcNow;

				var updateResult = await _addressRepository.UpdateAddressAsync(existingAddress);
				if (!updateResult)
				{
					await transaction.RollbackAsync();
					return Result<CustomerAddressDto>.Fail("Failed to update address", 500);
				}

				// Log admin operation
				var adminLog = await _adminOperationServices.AddAdminOpreationAsync(
					$"Updated address {addressId} for customer {userId}",
					Opreations.UpdateOpreation,
					userId,
					addressId
				);

				if (!adminLog.Success)
				{
					_logger.LogWarning($"Failed to log admin operation: {adminLog.Message}");
				}

				await _unitOfWork.CommitAsync();
				await transaction.CommitAsync();

				// Clear cache
				await _cacheManager.RemoveByTagAsync(CACHE_TAG_ADDRESS);

				var resultDto = _addressMapper.ToDto(existingAddress);
				return Result<CustomerAddressDto>.Ok(resultDto, "Address updated successfully", 200);
			}
			catch (Exception ex)
			{
				await transaction.RollbackAsync();
				_logger.LogError($"Error updating address {addressId}: {ex.Message}");
				NotifyAdminOfError($"Error updating address {addressId}: {ex.Message}", ex.StackTrace);
				return Result<CustomerAddressDto>.Fail("An error occurred while updating address", 500);
			}
		}

		public async Task<Result<string>> DeleteAddressAsync(int addressId, string userId)
		{
			_logger.LogInformation($"Deleting address {addressId} for user: {userId}");

			using var transaction = await _unitOfWork.BeginTransactionAsync();
			try
			{
				// Check if address exists and user owns it
				var addressExists = await _addressRepository.AddressExistsAsync(addressId, userId);
				if (!addressExists)
				{
					await transaction.RollbackAsync();
					return Result<string>.Fail($"Address with ID {addressId} not found or unauthorized", 404);
				}

				var deleteResult = await _addressRepository.DeleteAddressAsync(addressId, userId);
				if (!deleteResult)
				{
					await transaction.RollbackAsync();
					return Result<string>.Fail("Failed to delete address", 500);
				}

				// Log admin operation
				var adminLog = await _adminOperationServices.AddAdminOpreationAsync(
					$"Deleted address {addressId} for customer {userId}",
					Opreations.DeleteOpreation,
					userId,
					addressId
				);

				if (!adminLog.Success)
				{
					_logger.LogWarning($"Failed to log admin operation: {adminLog.Message}");
				}

				await _unitOfWork.CommitAsync();
				await transaction.CommitAsync();

				// Clear cache
				await _cacheManager.RemoveByTagAsync(CACHE_TAG_ADDRESS);

				return Result<string>.Ok(null, "Address deleted successfully", 200);
			}
			catch (Exception ex)
			{
				await transaction.RollbackAsync();
				_logger.LogError($"Error deleting address {addressId}: {ex.Message}");
				NotifyAdminOfError($"Error deleting address {addressId}: {ex.Message}", ex.StackTrace);
				return Result<string>.Fail("An error occurred while deleting address", 500);
			}
		}

		public async Task<Result<string>> SetDefaultAddressAsync(int addressId, string userId)
		{
			_logger.LogInformation($"Setting address {addressId} as default for user: {userId}");

			using var transaction = await _unitOfWork.BeginTransactionAsync();
			try
			{
				// Check if address exists and user owns it
				var addressExists = await _addressRepository.AddressExistsAsync(addressId, userId);
				if (!addressExists)
				{
					await transaction.RollbackAsync();
					return Result<string>.Fail($"Address with ID {addressId} not found or unauthorized", 404);
				}

				var setDefaultResult = await _addressRepository.SetDefaultAddressAsync(addressId, userId);
				if (!setDefaultResult)
				{
					await transaction.RollbackAsync();
					return Result<string>.Fail("Failed to set default address", 500);
				}

				// Log admin operation
				var adminLog = await _adminOperationServices.AddAdminOpreationAsync(
					$"Set address {addressId} as default for customer {userId}",
					Opreations.UpdateOpreation,
					userId,
					addressId
				);

				if (!adminLog.Success)
				{
					_logger.LogWarning($"Failed to log admin operation: {adminLog.Message}");
				}

				await _unitOfWork.CommitAsync();
				await transaction.CommitAsync();

				// Clear cache
				await _cacheManager.RemoveByTagAsync(CACHE_TAG_ADDRESS);

				return Result<string>.Ok(null, "Default address set successfully", 200);
			}
			catch (Exception ex)
			{
				await transaction.RollbackAsync();
				_logger.LogError($"Error setting default address {addressId}: {ex.Message}");
				NotifyAdminOfError($"Error setting default address {addressId}: {ex.Message}", ex.StackTrace);
				return Result<string>.Fail("An error occurred while setting default address", 500);
			}
		}

		public async Task<Result<List<CustomerAddressDto>>> GetAddressesByTypeAsync(string addressType, string userId)
		{
			_logger.LogInformation($"Getting addresses by type {addressType} for user: {userId}");

			try
			{
				var addresses = await _addressRepository.GetAddressesByTypeAsync(userId, addressType);
				var addressDtos = _addressMapper.ToDtos(addresses);
				return Result<List<CustomerAddressDto>>.Ok(addressDtos, $"Retrieved {addressDtos.Count} {addressType} addresses", 200);
			}
			catch (Exception ex)
			{
				_logger.LogError($"Error getting addresses by type {addressType} for user {userId}: {ex.Message}");
				NotifyAdminOfError($"Error getting addresses by type {addressType} for user {userId}: {ex.Message}", ex.StackTrace);
				return Result<List<CustomerAddressDto>>.Fail("An error occurred while retrieving addresses by type", 500);
			}
		}

		public async Task<Result<List<CustomerAddressDto>>> SearchAddressesAsync(string searchTerm, string userId)
		{
			_logger.LogInformation($"Searching addresses for user {userId} with term: {searchTerm}");

			try
			{
				var addresses = await _addressRepository.SearchAddressesAsync(userId, searchTerm);
				var addressDtos = _addressMapper.ToDtos(addresses);
				return Result<List<CustomerAddressDto>>.Ok(addressDtos, $"Found {addressDtos.Count} addresses matching '{searchTerm}'", 200);
			}
			catch (Exception ex)
			{
				_logger.LogError($"Error searching addresses for user {userId}: {ex.Message}");
				NotifyAdminOfError($"Error searching addresses for user {userId}: {ex.Message}", ex.StackTrace);
				return Result<List<CustomerAddressDto>>.Fail("An error occurred while searching addresses", 500);
			}
		}

		public async Task<Result<int?>> GetAddressCountAsync(string userId)
		{
			_logger.LogInformation($"Getting address count for user: {userId}");

			try
			{
				var count = await _addressRepository.GetAddressCountByCustomerAsync(userId);
				return Result<int?>.Ok(count, $"User has {count} addresses", 200);
			}
			catch (Exception ex)
			{
				_logger.LogError($"Error getting address count for user {userId}: {ex.Message}");
				NotifyAdminOfError($"Error getting address count for user {userId}: {ex.Message}", ex.StackTrace);
				return Result<int?>.Fail("An error occurred while getting address count", 500);
			}
		}

		public async Task<Result<CustomerAddressDto>> GetAddressWithCustomerAsync(int addressId, string userRole)
		{
			_logger.LogInformation($"Getting address {addressId} with customer details for role: {userRole}");

			try
			{
				// Only admins can access customer details
				if (userRole != "Admin")
				{
					return Result<CustomerAddressDto>.Fail("Unauthorized access", 403);
				}

				var address = await _addressRepository.GetAddressWithCustomerAsync(addressId);
				if (address == null)
				{
					return Result<CustomerAddressDto>.Fail($"Address with ID {addressId} not found", 404);
				}

				var addressDto = new CustomerAddressDto
				{
					Id = address.Id,
					CustomerId = address.CustomerId,
					PhoneNumber = address.PhoneNumber,
					Country = address.Country,
					State = address.State,
					City = address.City,
					StreetAddress = address.StreetAddress,
					ApartmentSuite = null,
					PostalCode = address.PostalCode,
					AddressType = address.AddressType,
					IsDefault = address.IsDefault,
					AdditionalNotes = address.AdditionalNotes,
					CreatedAt = address.CreatedAt,
					ModifiedAt = address.ModifiedAt,
					DeletedAt = address.DeletedAt
				};
				return Result<CustomerAddressDto>.Ok(addressDto, "Address with customer details retrieved successfully", 200);
			}
			catch (Exception ex)
			{
				_logger.LogError($"Error getting address {addressId} with customer details: {ex.Message}");
				NotifyAdminOfError($"Error getting address {addressId} with customer details: {ex.Message}", ex.StackTrace);
				return Result<CustomerAddressDto>.Fail("An error occurred while retrieving address with customer details", 500);
			}
		}
	}
}

