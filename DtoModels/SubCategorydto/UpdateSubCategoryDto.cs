using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace E_Commerce.DtoModels.SubCategorydto
{
    public class UpdateSubCategoryDto
    {
        [StringLength(20, ErrorMessage = "Name must be between 0 and 20 characters.")]
        [RegularExpression(@"^[a-zA-Z0-9][a-zA-Z0-9\s\-,]*[a-zA-Z0-9]$", ErrorMessage = "Name must start and end with an alphanumeric character and can contain spaces, hyphens, and commas in between.")]
        public string? Name { get; set; } = string.Empty;

        [StringLength(50, ErrorMessage = "Description must be between 0 and 50 characters.")]
        [RegularExpression(@"^[\w\s.,\-()'\""]{0,500}$", ErrorMessage = "Description can contain up to 500 characters: letters, numbers, spaces, and .,-()'\"")]
        public string? Description { get; set; } = string.Empty;
        public int? CategoryId { get; set; }
        

    }
} 