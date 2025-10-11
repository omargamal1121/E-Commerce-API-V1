using E_Commerce.DtoModels.CustomerAddressDtos;

namespace E_Commerce.DtoModels.AccountDtos
{

 
		public class Userdto
		{
			public string? Id { get; set; }
			public string? UserName { get; set; }
			public string? Email { get; set; }
			public string? Name { get; set; }
			public string?PhoneNumber { get; set; }

			public bool IsActive { get; set; }
			public bool IsDeleted { get; set; }
			public DateTime? LastVisit { get; set; }
		public DateTime CreateAt { get; set; }

		public bool IsLock { get; set; }
			public List<string> Roles { get; set; }
		}
		public class UserwithAddressdto
    {
			public string? Id { get; set; }
			public string? UserName { get; set; }
			public string? Email { get; set; }
			public string? Name { get; set; }
			public string?PhoneNumber { get; set; }

			public bool IsActive { get; set; }
			public bool IsDeleted { get; set; }
			public DateTime? LastVisit { get; set; }
			public bool IsLock { get; set; }
			public List<string> Roles { get; set; }
			public List<CustomerAddressDto> customerAddresses { get; set; }
    }
	}
