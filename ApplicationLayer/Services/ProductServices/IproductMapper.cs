using ApplicationLayer.DtoModels.ProductDtos;
using DomainLayer.Models;

namespace ApplicationLayer.Services.ProductServices
{
	public interface IproductMapper
	{
		public IQueryable<ProductDto> maptoProductDtoexpression(IQueryable<Product> query,bool IsAdmin=false);
		public IQueryable<ProductDetailDto> maptoProductDetailDtoexpression(IQueryable<Product> query, bool IsAdmin = false);

		public ProductDto Maptoproductdto(DomainLayer.Models.Product p, bool IsAdmin = false);

	}
}


