using E_Commerce.DtoModels.ProductDtos;
using E_Commerce.Models;

namespace E_Commerce.Services.ProductServices
{
	public interface IproductMapper
	{
		public IQueryable<ProductDto> maptoProductDtoexpression(IQueryable<Product> query);
		public IQueryable<ProductDetailDto> maptoProductDetailDtoexpression(IQueryable<Product> query);

		public ProductDto Maptoproductdto(E_Commerce.Models.Product p);

	}
}
