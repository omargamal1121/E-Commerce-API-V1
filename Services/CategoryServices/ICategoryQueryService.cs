using E_Commerce.DtoModels.CategoryDtos;

namespace E_Commerce.Services.CategoryServices
{
	public interface ICategoryQueryService
	{
		Task<Result<CategorywithdataDto>> GetCategoryByIdAsync(int id, bool? isActive , bool? IsDeleted );
		 Task<Result<List<CategoryDto>>> FilterAsync(string? search,bool? isActive = null,bool? isDeleted = null,int page = 1,int pageSize = 10);
	}
}
