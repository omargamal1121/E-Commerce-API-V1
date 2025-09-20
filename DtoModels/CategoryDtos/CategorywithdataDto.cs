using E_Commerce.DtoModels.SubCategorydto;

namespace E_Commerce.DtoModels.CategoryDtos
{
	public class CategorywithdataDto : CategoryDto 
	{
		public List<SubCategoryDto> SubCategories { get; set; }
	}
}
