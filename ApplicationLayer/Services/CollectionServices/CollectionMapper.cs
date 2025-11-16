
using ApplicationLayer.DtoModels.CollectionDtos;
using ApplicationLayer.DtoModels.ImagesDtos;
using ApplicationLayer.DtoModels.ProductDtos;
using DomainLayer.Models;

namespace ApplicationLayer.Services.CollectionServices
{
    public class CollectionMapper : ICollectionMapper
    {
        public IQueryable<CollectionDto> CollectionSelectorWithData(IQueryable<Collection> collections, bool IsAdmin = false)
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
                TotalProducts = IsAdmin
                    ? c.ProductCollections.Count()
                    : c.ProductCollections.Count(pc => pc.Product.IsActive && pc.Product.DeletedAt == null),
                Images = c.Images.Where(i => i.DeletedAt == null).Select(i => new ImageDto
                {
                    Id = i.Id,
                    Url = i.Url,
                    IsMain = i.IsMain
                }).ToList(),
                Products = (IsAdmin
                        ? c.ProductCollections
                        : c.ProductCollections.Where(p => p.Product.IsActive && p.Product.DeletedAt == null))
                    .Select(p => new ProductDto
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
                }).ToList()
            });
        }

        public IQueryable<CollectionSummaryDto> CollectionSelector(IQueryable<Collection> queryable)
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
                images = c.Images.Where(i=>i.DeletedAt==null).Select(i => new ImageDto
                {
                    Id = i.Id,
                    Url = i.Url,
                    IsMain = i.IsMain
                }).ToList()
            });
        }

        public CollectionDto ToCollectionDto(Collection c, bool IsAdmin = false) => new CollectionDto
        {
            Id = c.Id,
            Name = c.Name,
            Description = c.Description,
            IsActive = c.IsActive,
            CreatedAt = c.CreatedAt,
            DeletedAt = c.DeletedAt,
            ModifiedAt = c.ModifiedAt,
            DisplayOrder = c.DisplayOrder,
            TotalProducts = IsAdmin
                ? (c.ProductCollections?.Count() ?? 0)
                : (c.ProductCollections?.Count(pc => pc.Product.IsActive && pc.Product.DeletedAt == null) ?? 0),
            Images = c.Images?.Where(i=>i.DeletedAt==null).Select(i => new ImageDto
            {
                Id = i.Id,
                Url = i.Url,
                IsMain = i.IsMain
            }).ToList() ?? new List<ImageDto>(),
            Products = (IsAdmin
                    ? c.ProductCollections
                    : c.ProductCollections?.Where(p => p.Product.IsActive && p.Product.DeletedAt == null))
                ?.Select(p => new ProductDto
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
            })
            // For non-admins, we already filtered above by product status; keep all mapped items.
            ?.ToList() ?? new List<ProductDto>()
        };

        public CollectionSummaryDto ToCollectionSummaryDto(Collection c) => new CollectionSummaryDto
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


