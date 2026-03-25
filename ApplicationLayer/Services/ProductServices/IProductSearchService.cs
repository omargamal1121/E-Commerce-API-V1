using ApplicationLayer.DtoModels.ProductDtos;
using ApplicationLayer.DtoModels.CollectionDtos;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ApplicationLayer.Services.ProductServices
{
	public interface IProductSearchService
    {
        public Task<Result<List<BestSellingProductDto>>> GetBestSellerProductsWithCountAsync(bool? isDeleted, bool? isActive, int page = 1, int pagesize = 10, bool IsAdmin = false);
        Task<Result<List<ProductDto>>> GetNewArrivalsAsync(int page, int pageSize, bool? isActive = null, bool? deletedOnly = null, bool IsAdmin = false);
        Task<Result<List<ProductDto>>> GetBestSellersAsync(int page, int pageSize, bool? isActive = null, bool? deletedOnly = null, bool IsAdmin = false);
        Task<Result<List<ProductDto>>> AdvancedSearchAsync(AdvancedSearchDto searchCriteria, int page, int pageSize, bool? isActive = null, bool? deletedOnly = null, bool IsAdmin = false);
        Task<Result<List<BestSellingProductDto>>> GetMostWishlistedProductsAsync(int page = 1, int pageSize = 10, bool? isActive = null, bool? deletedOnly = null, bool IsAdmin = false);
        Task<Result<ProductSalesDto>> GetProductSalesAsync(int productId);
    }
}
