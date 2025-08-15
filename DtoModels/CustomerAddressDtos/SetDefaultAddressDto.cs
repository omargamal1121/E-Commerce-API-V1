using System.ComponentModel.DataAnnotations;

namespace E_Commerce.DtoModels.CustomerAddressDtos
{
	public class SetDefaultAddressDto
	{
		[Required(ErrorMessage = "Address ID Required")]
		[Range(1, int.MaxValue, ErrorMessage = "Address ID must be greater than 0")]
		public int AddressId { get; set; }
	}
} 