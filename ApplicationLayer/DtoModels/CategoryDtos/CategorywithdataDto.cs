using Application.DtoModels.SubCategorydto;

namespace Application.DtoModels.CategoryDtos
{
	public class CategorywithdataDto : CategoryDto 
	{
		public List<SubCategoryDto> SubCategories { get; set; }
	}
}


