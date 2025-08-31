using E_Commerce.DtoModels.CartDtos;
using E_Commerce.Models;
using System.Linq.Expressions;

namespace E_Commerce.Services.CartServices
{
    public interface ICartMapper
    {
        Expression<Func<Cart, CartDto>> CartDtoSelector { get; }
        CartDto ToCartDto(Cart cart);
    }
}
