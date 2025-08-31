using E_Commerce.DtoModels.DiscoutDtos;
using E_Commerce.DtoModels.ProductDtos;

namespace E_Commerce.Services.ProductServices
{
	public interface IProductDiscountService
	{
		Task<Result<DiscountDto>> GetProductDiscountAsync(int productId);
		Task<Result<bool>> AddDiscountToProductAsync(int productId, int discountId, string userId);
		Task<Result<bool>> UpdateProductDiscountAsync(int productId, int discountId, string userId);
		Task<Result<bool>> RemoveDiscountFromProductAsync(int productId, string userId);
		Task<Result<List<ProductDto>>> GetProductsWithActiveDiscountsAsync(bool ? IsActive);
		Task<Result<List<ProductDto>>> ApplyDiscountToProductsAsync(ApplyDiscountToProductsDto dto, string userId);
		Task<Result<List<ProductDto>>> RemoveDiscountFromProductsAsync(List<int> productIds, string userId);
	}
} 