using System.Linq.Expressions;
using E_Commerce.DtoModels.OrderDtos;
using E_Commerce.Models;

namespace E_Commerce.Services.Order
{
    public interface IOrderMapper
    {
        Expression<Func<Models.Order, OrderListDto>> OrderListSelector { get; }
        Expression<Func<Models.Order, OrderDto>> OrderSelector { get; }
    }
}
