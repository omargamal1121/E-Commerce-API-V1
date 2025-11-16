using System.Linq.Expressions;
using ApplicationLayer.DtoModels.OrderDtos;
using DomainLayer.Models;

namespace ApplicationLayer.Services.OrderService
{
    public interface IOrderMapper
    {
        Expression<Func<Order, OrderListDto>> OrderListSelector { get; }
        Expression<Func<Order, OrderDto>> OrderSelector { get; }
    }
}


