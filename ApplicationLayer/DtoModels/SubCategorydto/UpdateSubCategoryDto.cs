using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace Application.DtoModels.SubCategorydto
{
    public class UpdateSubCategoryDto
    {
        [StringLength(20, ErrorMessage = "Name must be between 0 and 20 characters.")]
       public string? Name { get; set; } = string.Empty;

      public string? Description { get; set; } = string.Empty;
        public int? CategoryId { get; set; }
        

    }
} 

