using E_Commerce.DtoModels.ImagesDtos;
using E_Commerce.DtoModels.SubCategorydto;
using E_Commerce.Services;
using Microsoft.AspNetCore.Http;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace E_Commerce.Interfaces
{
    public interface ISubCategoryServices
    {

		Task<Result<List<SubCategoryDto>>> FilterAsync(string? search, bool? isActive, bool? isDeleted, int page, int pageSize, bool IsAdmin = false);

		Task<Result<SubCategoryDto>> CreateAsync(CreateSubCategoryDto subCategory, string userid);
        Task<Result<bool>> DeleteAsync(int id, string userid);
        Task<Result<SubCategoryDto>> UpdateAsync(int subCategoryId, UpdateSubCategoryDto subCategory, string userid);
        Task<Result<SubCategoryDto>> ReturnRemovedSubCategoryAsync(int id, string userid);
        Task<Result<List<ImageDto>>> AddImagesToSubCategoryAsync(int subCategoryId, List<IFormFile> images, string userId);
        Task<Result<ImageDto>> AddMainImageToSubCategoryAsync(int subCategoryId, IFormFile mainImage, string userId);
        Task<Result<bool>> RemoveImageFromSubCategoryAsync(int subCategoryId, int imageId, string userId);
        Task<Result<bool>> ActivateSubCategoryAsync(int subCategoryId, string userId);
        Task<Result<bool>> DeactivateSubCategoryAsync(int subCategoryId, string userId);
        Task<Result<SubCategoryDtoWithData>> GetSubCategoryByIdAsync(int id, bool? isActive = null, bool? isDeleted = null, bool IsAdmin = false);

		Task DeactivateSubCategoryIfAllProductsAreInactiveAsync(int subCategoryId, string userId);
    }
} 