using E_Commerce.Context;
using E_Commerce.Interfaces;
using E_Commerce.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace E_Commerce.Repository
{
	public class CustomerAddressRepository : MainRepository<CustomerAddress>, ICustomerAddressRepository
	{
		private readonly AppDbContext _context;
		private readonly ILogger<CustomerAddressRepository> _logger;

		public CustomerAddressRepository(AppDbContext context, ILogger<CustomerAddressRepository> logger) 
			: base(context, logger)
		{
			_context = context;
			_logger = logger;
		}

		public async Task<CustomerAddress?> GetAddressByIdAsync(int addressId)
		{
			_logger.LogInformation($"Getting address by ID: {addressId}");
			
			return await _context.CustomerAddresses
				.Where(a => a.Id == addressId && a.DeletedAt == null)
				.Include(a => a.Customer)
				.FirstOrDefaultAsync();
		}
		public async Task<bool> IsExsistByIdAndUserIdAsync(int addressId, string customerId)
		{
			_logger.LogInformation($"Checking if address {addressId} exists for customer: {customerId}");
			
			return await _context.CustomerAddresses
				.AnyAsync(a => a.Id == addressId && a.CustomerId == customerId && a.DeletedAt == null);
		}
		public async Task<List<CustomerAddress>> GetAddressesByCustomerAsync(string customerId)
		{
			_logger.LogInformation($"Getting addresses for customer: {customerId}");
			
			return await _context.CustomerAddresses
				.Where(a => a.CustomerId == customerId && a.DeletedAt == null)
				.Include(a => a.Customer)
				.OrderByDescending(a => a.IsDefault)
				.ThenBy(a => a.CreatedAt)
				.ToListAsync();
		}

		public async Task<CustomerAddress?> GetDefaultAddressAsync(string customerId)
		{
			_logger.LogInformation($"Getting default address for customer: {customerId}");
			
			return await _context.CustomerAddresses
				.Where(a => a.CustomerId == customerId && a.IsDefault && a.DeletedAt == null)
				.Include(a => a.Customer)
				.FirstOrDefaultAsync();
		}

		public async Task<bool> SetDefaultAddressAsync(int addressId, string customerId)
		{
			_logger.LogInformation($"Setting address {addressId} as default for customer: {customerId}");
			
			try
			{
				var address = await _context.CustomerAddresses
					.Where(a => a.Id == addressId && a.CustomerId == customerId && a.DeletedAt == null)
					.FirstOrDefaultAsync();

				if (address == null)
				{
					_logger.LogWarning($"Address {addressId} not found for customer {customerId}");
					return false;
				}

				// Remove default from other addresses
				await RemoveDefaultFromOtherAddressesAsync(customerId, addressId);

				// Set this address as default
				address.IsDefault = true;
				address.ModifiedAt = DateTime.UtcNow;

				_context.CustomerAddresses.Update(address);
				return true;
			}
			catch (Exception ex)
			{
				_logger.LogError($"Error setting default address: {ex.Message}");
				return false;
			}
		}

		public async Task<bool> RemoveDefaultFromOtherAddressesAsync(string customerId, int excludeAddressId)
		{
			_logger.LogInformation($"Removing default from other addresses for customer: {customerId}");
			
			try
			{
				var addresses = await _context.CustomerAddresses
					.Where(a => a.CustomerId == customerId && a.Id != excludeAddressId && a.IsDefault && a.DeletedAt == null)
					.ToListAsync();

				foreach (var address in addresses)
				{
					address.IsDefault = false;
					address.ModifiedAt = DateTime.UtcNow;
				}

				if (addresses.Any())
				{
					_context.CustomerAddresses.UpdateRange(addresses);
				}

				return true;
			}
			catch (Exception ex)
			{
				_logger.LogError($"Error removing default from other addresses: {ex.Message}");
				return false;
			}
		}

		public async Task<int> GetAddressCountByCustomerAsync(string customerId)
		{
			_logger.LogInformation($"Getting address count for customer: {customerId}");
			
			return await _context.CustomerAddresses
				.Where(a => a.CustomerId == customerId && a.DeletedAt == null)
				.CountAsync();
		}

		public async Task<bool> AddressExistsAsync(int addressId, string customerId)
		{
			_logger.LogInformation($"Checking if address {addressId} exists for customer: {customerId}");
			
			return await _context.CustomerAddresses
				.AnyAsync(a => a.Id == addressId && a.CustomerId == customerId && a.DeletedAt == null);
		}

		public async Task<List<CustomerAddress>> GetAddressesByTypeAsync(string customerId, string addressType)
		{
			_logger.LogInformation($"Getting addresses by type {addressType} for customer: {customerId}");
			
			return await _context.CustomerAddresses
				.Where(a => a.CustomerId == customerId && a.AddressType == addressType && a.DeletedAt == null)
				.Include(a => a.Customer)
				.OrderByDescending(a => a.IsDefault)
				.ThenBy(a => a.CreatedAt)
				.ToListAsync();
		}

		public async Task<bool> UpdateAddressAsync(CustomerAddress address)
		{
			_logger.LogInformation($"Updating address: {address.Id}");
			
			try
			{
				address.ModifiedAt = DateTime.UtcNow;
				_context.CustomerAddresses.Update(address);
				return true;
			}
			catch (Exception ex)
			{
				_logger.LogError($"Error updating address: {ex.Message}");
				return false;
			}
		}

		public async Task<bool> DeleteAddressAsync(int addressId, string customerId)
		{
			_logger.LogInformation($"Deleting address {addressId} for customer: {customerId}");
			
			try
			{
				var address = await _context.CustomerAddresses
					.Where(a => a.Id == addressId && a.CustomerId == customerId && a.DeletedAt == null)
					.FirstOrDefaultAsync();

				if (address == null)
				{
					_logger.LogWarning($"Address {addressId} not found for customer {customerId}");
					return false;
				}

				address.DeletedAt = DateTime.UtcNow;
				address.ModifiedAt = DateTime.UtcNow;

				// If this was the default address, set another address as default
				if (address.IsDefault)
				{
					var newDefault = await _context.CustomerAddresses
						.Where(a => a.CustomerId == customerId && a.Id != addressId && a.DeletedAt == null)
						.OrderBy(a => a.CreatedAt)
						.FirstOrDefaultAsync();

					if (newDefault != null)
					{
						newDefault.IsDefault = true;
						newDefault.ModifiedAt = DateTime.UtcNow;
						_context.CustomerAddresses.Update(newDefault);
					}
				}

				_context.CustomerAddresses.Update(address);
				return true;
			}
			catch (Exception ex)
			{
				_logger.LogError($"Error deleting address: {ex.Message}");
				return false;
			}
		}

		public async Task<CustomerAddress?> GetAddressWithCustomerAsync(int addressId)
		{
			_logger.LogInformation($"Getting address with customer details: {addressId}");
			
			return await _context.CustomerAddresses
				.Where(a => a.Id == addressId && a.DeletedAt == null)
				.Include(a => a.Customer)
				.FirstOrDefaultAsync();
		}

		public async Task<List<CustomerAddress>> SearchAddressesAsync(string customerId, string searchTerm)
		{
			_logger.LogInformation($"Searching addresses for customer {customerId} with term: {searchTerm}");
			
			if (string.IsNullOrWhiteSpace(searchTerm))
				return await GetAddressesByCustomerAsync(customerId);

			return await _context.CustomerAddresses
				.Where(a => a.CustomerId == customerId && a.DeletedAt == null &&
					(
					 a.City.Contains(searchTerm) || 
					 a.State.Contains(searchTerm) || 
					 a.Country.Contains(searchTerm) ||
					 a.StreetAddress.Contains(searchTerm)))
				.Include(a => a.Customer)
				.OrderByDescending(a => a.IsDefault)
				.ThenBy(a => a.CreatedAt)
				.ToListAsync();
		}
	}
} 