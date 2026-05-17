using Application.DtoModels.ProductDtos;


namespace Application.DtoModels.SubCategorydto
{
	public class SubCategoryDtoWithData: SubCategoryDto
	{
		public IEnumerable<ProductDto>? Products { get; set; }
	}
}


