using Application.DtoModels.DiscoutDtos;
using Domain.Models;
using System.Linq.Expressions;

namespace Application.Services.DiscountServices
{
    public interface IDiscountMapper
    {
        Expression<Func<Discount, DiscountDto>> DiscountDtoSelector { get; }
        Expression<Func<Discount, DiscountDto>> DiscountsDtoSelector { get; }
        DiscountDto ToDiscountDto(Discount discount);
    }
}


