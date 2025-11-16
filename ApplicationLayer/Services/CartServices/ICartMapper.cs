using ApplicationLayer.DtoModels.CartDtos;
using DomainLayer.Models;
using System.Linq.Expressions;

namespace ApplicationLayer.Services.CartServices
{
    public interface ICartMapper
    {
        Expression<Func<Cart, CartDto>> CartDtoSelector { get; }
        CartDto ToCartDto(Cart cart);
    }
}


