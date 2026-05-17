using System.ComponentModel.DataAnnotations;

namespace Application.DtoModels.AccountDtos
{
    public class ConfirmEmailDto
    {
        [Required(ErrorMessage = "Token is required")]
        public string Token { get; set; }
    }
} 

