using E_Commerce.DtoModels.ImagesDtos;
using E_Commerce.DtoModels.ProductDtos;
using E_Commerce.DtoModels.Shared;

namespace E_Commerce.DtoModels.CollectionDtos
{
    public class CollectionDto : BaseDto
    {
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public int DisplayOrder { get; set; }
        public bool IsActive { get; set; }
        public IEnumerable<ProductDto> Products { get; set; }
        public IEnumerable<ImageDto> Images { get; set; } 
       
        public int TotalProducts { get; set; }
    }

    public class CreateCollectionDto
    {
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public int DisplayOrder { get; set; }
       
    }

    public class UpdateCollectionDto
    {
        public string? Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public int? DisplayOrder { get; set; }
     
    }

    public class AddProductsToCollectionDto
    {
        public List<int> ProductIds { get; set; } = new List<int>();
    }

    public class RemoveProductsFromCollectionDto
    {
        public List<int> ProductIds { get; set; } = new List<int>();
    }

    public class CollectionSummaryDto : BaseDto
    {
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public int DisplayOrder { get; set; }
        public bool IsActive { get; set; }
        public IEnumerable<ImageDto>? images { get; set; }
        public int TotalProducts { get; set; }

    }
} 