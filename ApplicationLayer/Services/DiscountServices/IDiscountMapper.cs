using ApplicationLayer.DtoModels.DiscoutDtos;
using DomainLayer.Models;
using System.Linq.Expressions;

namespace ApplicationLayer.Services.DiscountServices
{
    public interface IDiscountMapper
    {
        Expression<Func<Discount, DiscountDto>> DiscountDtoSelector { get; }
        Expression<Func<Discount, DiscountDto>> DiscountsDtoSelector { get; }
        DiscountDto ToDiscountDto(Discount discount);
    }
}


