using Application.DtoModels.CartDtos;
using Domain.Models;
using System.Linq.Expressions;

namespace Application.Services.CartServices
{
    public interface ICartMapper
    {
        Expression<Func<Cart, CartDto>> CartDtoSelector { get; }
        CartDto ToCartDto(Cart cart);
    }
}


