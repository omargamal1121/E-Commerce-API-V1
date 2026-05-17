using System.ComponentModel.DataAnnotations;

namespace Application.DtoModels.AccountDtos
{
    public class RequestPasswordResetDto
    {
        [Required]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;
    }
} 

