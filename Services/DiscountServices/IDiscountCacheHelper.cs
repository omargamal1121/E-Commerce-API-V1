using E_Commerce.DtoModels.DiscoutDtos;

namespace E_Commerce.Services.DiscountServices
{
    public interface IDiscountCacheHelper
    {
        void ClearProductCache();
        public void SetCache(List<DiscountDto> discountDto,
          
          bool? isActive = null,
          bool? isDeleted = null,
          string? Searchkey = null,
          bool? IsAdmin = false,
          int? page = null,
          int? PageSize = null);
        public void SetCache(DiscountDto discountDto,
          int? id = null,
          bool? isActive = null,
          bool? isDeleted = null,
          bool? IsAdmin = false);
          Task<List<DiscountDto>?> GetCacheAsync( bool? isActive = null, bool? isDeleted = null,string? SearchKey=null, bool? IsAdmin = false, int? page = null, int? PageSize = null);
          Task<DiscountDto?> GetCacheAsync(int? id = null, bool? isActive = null, bool? isDeleted = null, bool? IsAdmin = false);
    }
}
