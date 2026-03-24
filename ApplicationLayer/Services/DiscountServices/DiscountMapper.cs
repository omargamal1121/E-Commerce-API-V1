using ApplicationLayer.DtoModels.DiscoutDtos;
using ApplicationLayer.DtoModels.ImagesDtos;
using ApplicationLayer.DtoModels.ProductDtos;
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
            DeletedAt = discount.DeletedAt,
            products=discount.products.Select(p=> new ProductDto
            {
                Id = p.Id,
                Name = p.Name,
                FinalPrice= (p.Discount != null && p.Discount.IsActive && p.Discount.EndDate > DateTime.UtcNow) ? Math.Round(p.Price - (((p.Discount.DiscountPercent) / 100) * p.Price)) : p.Price,
                 Price = p.Price,
                 images = p.Images.Where(i => i.DeletedAt == null).Select(i => new ImageDto { Id = i.Id, Url = i.Url }).ToList(),
                 IsActive = p.IsActive,
				Description = p.Description,
                AvailableQuantity = p.Quantity
            }).ToList()

		};
        public Expression<Func<Discount, DiscountDto>> DiscountsDtoSelector => discount => new DiscountDto
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


