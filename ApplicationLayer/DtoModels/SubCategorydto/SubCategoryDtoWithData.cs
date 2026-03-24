using ApplicationLayer.DtoModels.ProductDtos;


namespace ApplicationLayer.DtoModels.SubCategorydto
{
	public class SubCategoryDtoWithData: SubCategoryDto
	{
		public List<ProductDto>? Products { get; set; }
	}
}


