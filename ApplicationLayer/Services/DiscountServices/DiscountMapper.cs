using ApplicationLayer.DtoModels.DiscoutDtos;
using DomainLayer.Models;
using System.Linq.Expressions;

namespace ApplicationLayer.Services.DiscountServices
{
    public class DiscountMapper : IDiscountMapper
    {
        public Expression<Func<Discount, DiscountDto>> DiscountDtoSelector => discount => new DiscountDto
        {
            Id = discount.Id,
            Name = discount.Name,
            Description = discount.Description,
            DiscountPercent = discount.DiscountPercent,
            StartDate = discount.StartDate,
            EndDate = discount.EndDate,
            IsActive = discount.IsActive,
            CreatedAt = discount.CreatedAt,
            ModifiedAt = discount.ModifiedAt,
            DeletedAt = discount.DeletedAt
        };

        public DiscountDto ToDiscountDto(Discount discount)
        {
            return DiscountDtoSelector.Compile()(discount);
        }
    }
}


