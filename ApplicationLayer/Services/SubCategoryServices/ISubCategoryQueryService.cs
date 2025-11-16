using ApplicationLayer.DtoModels.SubCategorydto;
using ApplicationLayer.Services;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ApplicationLayer.Services.SubCategoryServices
{
    public interface ISubCategoryQueryService
    {
        Task<Result<SubCategoryDtoWithData>> GetSubCategoryByIdAsync(int id, bool? isActive, bool? isDeleted,bool IsAdmin=false);
        Task<Result<List<SubCategoryDto>>> FilterAsync(string? search, bool? isActive = null, bool? isDeleted = null, int page = 1, int pageSize = 10, bool IsAdmin = false);
    }
}

