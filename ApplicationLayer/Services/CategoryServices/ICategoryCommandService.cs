using ApplicationLayer.DtoModels.CategoryDtos;

namespace ApplicationLayer.Services.CategoryServices
{
	public interface ICategoryCommandService
	{
		Task<Result<CategoryDto>> CreateAsync(CreateCategotyDto model, string userId);
		Task<Result<bool>> DeleteAsync(int id, string userId);
		Task<Result<bool>> ActivateAsync(int id, string userId);
		Task<Result<bool>> DeactivateAsync(int id, string userId);
		Task<Result<CategoryDto>> RestoreAsync(int id, string userId);
		Task<Result<CategoryDto>> UpdateAsync(int categoryId, UpdateCategoryDto category, string userid);
		public Task DeactivateCategoryIfNoActiveSubcategories(int categoryId, string userId);
	}
}


