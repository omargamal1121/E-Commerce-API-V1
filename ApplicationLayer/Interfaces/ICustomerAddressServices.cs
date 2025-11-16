using ApplicationLayer.DtoModels.CustomerAddressDtos;
using ApplicationLayer.DtoModels.Responses;
using ApplicationLayer.Services;

namespace ApplicationLayer.Interfaces
{
	public interface ICustomerAddressServices
	{
		Task<Result<CustomerAddressDto>> GetAddressByIdAsync(int addressId, string userId);
		Task<Result<List<CustomerAddressDto>>> GetCustomerAddressesAsync(string userId);
		Task<Result<CustomerAddressDto>> GetDefaultAddressAsync(string userId);
		Task<Result<CustomerAddressDto>> CreateAddressAsync(CreateCustomerAddressDto addressDto, string userId);
		Task<Result<CustomerAddressDto>> UpdateAddressAsync(int addressId, UpdateCustomerAddressDto addressDto, string userId);
		Task<Result<string>> DeleteAddressAsync(int addressId, string userId);
		Task<Result<string>> SetDefaultAddressAsync(int addressId, string userId);
		Task<Result<List<CustomerAddressDto>>> GetAddressesByTypeAsync(string addressType, string userId);
		Task<Result<List<CustomerAddressDto>>> SearchAddressesAsync(string searchTerm, string userId);
		Task<Result<int?>> GetAddressCountAsync(string userId);
		Task<Result<CustomerAddressDto>> GetAddressWithCustomerAsync(int addressId, string userRole);
	}
} 

