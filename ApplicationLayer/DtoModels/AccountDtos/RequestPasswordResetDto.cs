using System.ComponentModel.DataAnnotations;

namespace ApplicationLayer.DtoModels.AccountDtos
{
    public class RequestPasswordResetDto
    {
        [Required]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;
    }
} 

