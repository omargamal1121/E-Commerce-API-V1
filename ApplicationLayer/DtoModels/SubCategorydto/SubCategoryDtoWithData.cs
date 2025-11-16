using ApplicationLayer.DtoModels.ProductDtos;


namespace ApplicationLayer.DtoModels.SubCategorydto
{
	public class SubCategoryDtoWithData: SubCategoryDto
	{
		public IEnumerable<ProductDto>? Products { get; set; }
	}
}


