using System.ComponentModel.DataAnnotations;

namespace ApplicationLayer.DtoModels.AccountDtos
{
    public class ConfirmEmailDto
    {
        [Required(ErrorMessage = "Token is required")]
        public string Token { get; set; }
    }
} 

