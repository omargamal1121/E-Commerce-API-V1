using Application.DtoModels.ProductDtos;
using Domain.Models;

namespace Application.Services.ProductServices
{
	public interface IproductMapper
	{
		public IQueryable<ProductDto> maptoProductDtoexpression(IQueryable<Product> query,bool IsAdmin=false);
		public IQueryable<ProductDetailDto> maptoProductDetailDtoexpression(IQueryable<Product> query, bool IsAdmin = false);

		public ProductDto Maptoproductdto(Domain.Models.Product p, bool IsAdmin = false);

	}
}


