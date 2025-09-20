using E_Commerce.DtoModels.ProductDtos;
using E_Commerce.Models;

namespace E_Commerce.Services.ProductServices
{
	public interface IproductMapper
	{
		public IQueryable<ProductDto> maptoProductDtoexpression(IQueryable<Product> query,bool IsAdmin=false);
		public IQueryable<ProductDetailDto> maptoProductDetailDtoexpression(IQueryable<Product> query, bool IsAdmin = false);

		public ProductDto Maptoproductdto(E_Commerce.Models.Product p, bool IsAdmin = false);

	}
}
