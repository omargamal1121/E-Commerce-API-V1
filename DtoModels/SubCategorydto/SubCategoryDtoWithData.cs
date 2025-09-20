using E_Commerce.DtoModels.ProductDtos;


namespace E_Commerce.DtoModels.SubCategorydto
{
	public class SubCategoryDtoWithData: SubCategoryDto
	{
		public IEnumerable<ProductDto>? Products { get; set; }
	}
}
