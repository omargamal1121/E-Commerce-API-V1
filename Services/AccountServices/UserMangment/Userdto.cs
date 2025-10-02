namespace E_Commerce.Services.AccountServices.UserMangment
{

    public partial class UserQueryServiece
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
			public List<string> Roles { get; set; }
		}
	}
}
