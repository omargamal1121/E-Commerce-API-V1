using System.ComponentModel.DataAnnotations;

namespace E_Commerce.DtoModels.AccountDtos
{
    public class ResendConfirmationEmailDto
    {
        [Required(ErrorMessage = "Email is required")]
        [EmailAddress(ErrorMessage = "Invalid email format")]
        public string Email { get; set; }
    }
} 