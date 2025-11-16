using ApplicationLayer.DtoModels.CategoryDtos;

namespace ApplicationLayer.Services.CategoryServices
{
	public interface ICategoryQueryService
	{
		Task<Result<CategorywithdataDto>> GetCategoryByIdAsync(int id, bool? isActive , bool? IsDeleted,bool IsAdmin );
		 Task<Result<List<CategoryDto>>> FilterAsync(string? search,bool? isActive = null,bool? isDeleted = null,int page = 1,int pageSize = 10 ,bool IsAdmin=false);
	}
}


