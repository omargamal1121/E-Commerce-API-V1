using System.Linq.Expressions;
using Application.DtoModels.OrderDtos;
using Domain.Models;

namespace Application.Services.OrderServices
{
    public interface IOrderMapper
    {
        Expression<Func<Order, OrderListDto>> OrderListSelector { get; }
        Expression<Func<Order, OrderDto>> OrderSelector { get; }
    }
}


