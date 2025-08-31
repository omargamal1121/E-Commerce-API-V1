using E_Commerce.DtoModels.DiscoutDtos;
using E_Commerce.Models;
using System.Linq.Expressions;

namespace E_Commerce.Services.Discount
{
    public class DiscountMapper : IDiscountMapper
    {
        public Expression<Func<Models.Discount, DiscountDto>> DiscountDtoSelector => discount => new DiscountDto
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

        public DiscountDto ToDiscountDto(Models.Discount discount)
        {
            return DiscountDtoSelector.Compile()(discount);
        }
    }
}
