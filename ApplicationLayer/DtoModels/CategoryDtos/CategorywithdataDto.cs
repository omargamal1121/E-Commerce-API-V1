using ApplicationLayer.DtoModels.SubCategorydto;

namespace ApplicationLayer.DtoModels.CategoryDtos
{
	public class CategorywithdataDto : CategoryDto 
	{
		public List<SubCategoryDto> SubCategories { get; set; }
	}
}


