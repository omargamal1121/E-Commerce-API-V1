using ApplicationLayer.DtoModels.DiscoutDtos;
using DomainLayer.Models;
using System.Linq.Expressions;

namespace ApplicationLayer.Services.DiscountServices
{
    public interface IDiscountMapper
    {
        Expression<Func<Discount, DiscountDto>> DiscountDtoSelector { get; }
        DiscountDto ToDiscountDto(Discount discount);
    }
}


