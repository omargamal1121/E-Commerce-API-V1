using E_Commerce.DtoModels.SubCategorydto;
using E_Commerce.Models;
using E_Commerce.Services;

namespace E_Commerce.Services.SubCategoryServices
{
    public interface ISubCategoryCommandService
    {
		Task<Result<SubCategoryDto>> CreateAsync(CreateSubCategoryDto subCategory, string userid);
		Task<Result<bool>> DeleteAsync(int id, string userid);
		Task<Result<SubCategoryDto>> UpdateAsync(int subCategoryId, UpdateSubCategoryDto subCategory, string userid);
		Task<Result<SubCategoryDto>> ReturnRemovedSubCategoryAsync(int id, string userid);

		Task<Result<bool>> ActivateSubCategoryAsync(int subCategoryId, string userId);
		Task<Result<bool>> DeactivateSubCategoryAsync(int subCategoryId, string userId);
		Task DeactivateSubCategoryIfAllProductsAreInactiveAsync(int subCategoryId, string userId);
	}
}