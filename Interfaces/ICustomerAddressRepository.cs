using E_Commerce.Models;

namespace E_Commerce.Interfaces
{
	public interface ICustomerAddressRepository : IRepository<CustomerAddress>
	{
		Task<CustomerAddress?> GetAddressByIdAsync(int addressId);
		Task<List<CustomerAddress>> GetAddressesByCustomerAsync(string customerId);
		Task<CustomerAddress?> GetDefaultAddressAsync(string customerId);
		Task<bool> SetDefaultAddressAsync(int addressId, string customerId);
		Task<bool> RemoveDefaultFromOtherAddressesAsync(string customerId, int excludeAddressId);
		public  Task<bool> IsExsistByIdAndUserIdAsync(int addressId, string customerId);
		Task<int> GetAddressCountByCustomerAsync(string customerId);
		Task<bool> AddressExistsAsync(int addressId, string customerId);
		Task<List<CustomerAddress>> GetAddressesByTypeAsync(string customerId, string addressType);
		Task<bool> UpdateAddressAsync(CustomerAddress address);
		Task<bool> DeleteAddressAsync(int addressId, string customerId);
		Task<CustomerAddress?> GetAddressWithCustomerAsync(int addressId);
		Task<List<CustomerAddress>> SearchAddressesAsync(string customerId, string searchTerm);
	}
} 