using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace E_Commerce.DtoModels.AccountDtos
{
	public class ChangePasswordDto
	{

		[Required(ErrorMessage = "Current Password Is Required")]
		[DisplayName("Current Password")]
		public string CurrentPass { get; set; } = string.Empty;
		[Required(ErrorMessage = "New Password Is Required")]
		[DisplayName("New Password")]
		public string NewPass { get; set; } = string.Empty;
		[Required(ErrorMessage = "New Confirem Password Is Required")]
		[DisplayName("Current Confirem Password")]
		public string ConfirmNewPass { get; set; } = string.Empty;
	}
}
