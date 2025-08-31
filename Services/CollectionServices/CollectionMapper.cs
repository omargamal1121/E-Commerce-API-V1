using E_Commerce.DtoModels.CollectionDtos;
using E_Commerce.DtoModels.ImagesDtos;
using E_Commerce.DtoModels.ProductDtos;
using E_Commerce.Models;
using System.Linq.Expressions;

namespace E_Commerce.Services.Collection
{
    public class CollectionMapper : ICollectionMapper
    {
        public IQueryable<CollectionDto> CollectionSelectorWithData(IQueryable<Models.Collection> collections)
        {
            return collections.Select(c => new CollectionDto
            {
                Id = c.Id,
                Name = c.Name,
                Description = c.Description,
                IsActive = c.IsActive,
                CreatedAt = c.CreatedAt,
                DeletedAt = c.DeletedAt,
                ModifiedAt = c.ModifiedAt,
                DisplayOrder = c.DisplayOrder,
                TotalProducts = c.ProductCollections.Count(),
                Images = c.Images.Select(i => new ImageDto
                {
                    Id = i.Id,
                    Url = i.Url,
                    IsMain = i.IsMain
                }).ToList(),
                Products = c.ProductCollections.Where(p => p.Product.IsActive && p.Product.DeletedAt == null).Select(p => new ProductDto
                {
                    Id = p.ProductId,
                    Name = p.Product.Name,
                    Description = p.Product.Description,
                    AvailableQuantity = p.Product.Quantity,
                    Gender = p.Product.Gender,
                    SubCategoryId = p.Product.SubCategoryId,
                    Price = p.Product.Price,
                    CreatedAt = p.CreatedAt,
                    ModifiedAt = p.ModifiedAt,
                    DeletedAt = p.DeletedAt,
                    FinalPrice = (p.Product.Discount != null && p.Product.Discount.IsActive && (p.Product.Discount.DeletedAt == null) && (p.Product.Discount.EndDate > DateTime.UtcNow)) ? Math.Round(p.Product.Price - (((p.Product.Discount.DiscountPercent) / 100) * p.Product.Price)) : p.Product.Price,
                    fitType = p.Product.fitType,
                    images = p.Product.Images.Where(i => i.DeletedAt == null).Select(i => new ImageDto { Id = i.Id, Url = i.Url }).ToList(),
                    EndAt = (p.Product.Discount != null && p.Product.Discount.IsActive && p.Product.Discount.EndDate > DateTime.UtcNow) && p.Product.Discount.IsActive ? p.Product.Discount.EndDate : null,
                    DiscountName = (p.Product.Discount != null && p.Product.Discount.IsActive && p.Product.Discount.EndDate > DateTime.UtcNow) ? p.Product.Discount.Name : null,
                    DiscountPrecentage = (p.Product.Discount != null && p.Product.Discount.IsActive && p.Product.Discount.EndDate > DateTime.UtcNow) ? p.Product.Discount.DiscountPercent : 0,
                    IsActive = p.Product.IsActive,
                }).Where(p => p.IsActive && p.DeletedAt == null).ToList()
            });
        }

        public IQueryable<CollectionSummaryDto> CollectionSelector(IQueryable<Models.Collection> queryable)
        {
            return queryable.Select(c => new CollectionSummaryDto
            {
                Id = c.Id,
                Name = c.Name,
                Description = c.Description,
                IsActive = c.IsActive,
                CreatedAt = c.CreatedAt,
                DeletedAt = c.DeletedAt,
                ModifiedAt = c.ModifiedAt,
                DisplayOrder = c.DisplayOrder,
                TotalProducts = c.ProductCollections.Count(),
                images = c.Images.Select(i => new ImageDto
                {
                    Id = i.Id,
                    Url = i.Url,
                    IsMain = i.IsMain
                }).ToList()
            });
        }

        public CollectionDto ToCollectionDto(Models.Collection c) => new CollectionDto
        {
            Id = c.Id,
            Name = c.Name,
            Description = c.Description,
            IsActive = c.IsActive,
            CreatedAt = c.CreatedAt,
            DeletedAt = c.DeletedAt,
            ModifiedAt = c.ModifiedAt,
            DisplayOrder = c.DisplayOrder,
            TotalProducts = c.ProductCollections?.Count() ?? 0,
            Images = c.Images?.Select(i => new ImageDto
            {
                Id = i.Id,
                Url = i.Url,
                IsMain = i.IsMain
            }).ToList() ?? new List<ImageDto>(),
            Products = c.ProductCollections?.Where(p => p.Product.IsActive && p.Product.DeletedAt == null).Select(p => new ProductDto
            {
                Id = p.ProductId,
                Name = p.Product.Name,
                Description = p.Product.Description,
                AvailableQuantity = p.Product.Quantity,
                Gender = p.Product.Gender,
                SubCategoryId = p.Product.SubCategoryId,
                Price = p.Product.Price,
                CreatedAt = p.CreatedAt,
                ModifiedAt = p.ModifiedAt,
                DeletedAt = p.DeletedAt,
                FinalPrice = (p.Product.Discount != null && p.Product.Discount.IsActive && (p.Product.Discount.DeletedAt == null) && (p.Product.Discount.EndDate > DateTime.UtcNow)) ? Math.Round(p.Product.Price - (((p.Product.Discount.DiscountPercent) / 100) * p.Product.Price)) : p.Product.Price,
                fitType = p.Product.fitType,
                images = p.Product.Images?.Where(i => i.DeletedAt == null).Select(i => new ImageDto { Id = i.Id, Url = i.Url }).ToList() ?? new List<ImageDto>(),
                EndAt = (p.Product.Discount != null && p.Product.Discount.IsActive && p.Product.Discount.EndDate > DateTime.UtcNow) && p.Product.Discount.IsActive ? p.Product.Discount.EndDate : null,
                DiscountName = (p.Product.Discount != null && p.Product.Discount.IsActive && p.Product.Discount.EndDate > DateTime.UtcNow) ? p.Product.Discount.Name : null,
                DiscountPrecentage = (p.Product.Discount != null && p.Product.Discount.IsActive && p.Product.Discount.EndDate > DateTime.UtcNow) ? p.Product.Discount.DiscountPercent : 0,
                IsActive = p.Product.IsActive,
            }).Where(p => p.IsActive && p.DeletedAt == null).ToList() ?? new List<ProductDto>()
        };

        public CollectionSummaryDto ToCollectionSummaryDto(Models.Collection c) => new CollectionSummaryDto
        {
            Id = c.Id,
            Name = c.Name,
            Description = c.Description,
            DisplayOrder = c.DisplayOrder,
            IsActive = c.IsActive,
            CreatedAt = c.CreatedAt,
            ModifiedAt = c.ModifiedAt,
            DeletedAt = c.DeletedAt,
            TotalProducts = c.ProductCollections?.Count() ?? 0,
            images = c.Images?.Select(i => new ImageDto
            {
                Id = i.Id,
                Url = i.Url,
                IsMain = i.IsMain
            }).ToList() ?? new List<ImageDto>()
        };
    }
}
