using E_Commerce.DtoModels.DiscoutDtos;
using E_Commerce.Models;
using System.Linq.Expressions;

namespace E_Commerce.Services.DiscountServices
{
    public interface IDiscountMapper
    {
        Expression<Func<Models.Discount, DiscountDto>> DiscountDtoSelector { get; }
        DiscountDto ToDiscountDto(Models.Discount discount);
    }
}
