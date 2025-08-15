using E_Commerce.DtoModels.CustomerAddressDtos;
using E_Commerce.DtoModels.ImagesDtos;
using E_Commerce.Enums;

namespace E_Commerce.DtoModels.AccountDtos
{
	public class ProfileDto
	{
		public string Name { get; set; }
		public string Email { get; set; }
		public string PhoneNumber { get; set; }

		public ImageDto ProfileImage { get; set; }

		public string Gender { get; set; }

		public bool IsConfirmed { get; set; }

		public  string UserName { get; set; }

		public List<CustomerAddressDto>  customerAddresses { get; set; }

	}
}
