using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace Application.DtoModels.SubCategorydto
{
    public class CreateSubCategoryDto
    {
        [Required(ErrorMessage = "Name is required.")]
      public string Name { get; set; } = string.Empty;

        [Required(ErrorMessage = "Description is required.")]
         public string Description { get; set; } = string.Empty;

        [Required]
        public int CategoryId { get; set; }
    
    }
} 

