using System;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;
using ApplicationLayer.DtoModels.ProductDtos;
using ApplicationLayer.DtoModels.Responses;
using ApplicationLayer.Interfaces;
using DomainLayer.Models;
using ApplicationLayer.Services.Cache;
using Hangfire;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ApplicationLayer.DtoModels.ImagesDtos;

namespace ApplicationLayer.Services.WishlistServices
{
    public class WishlistQueryService : IWishlistQueryService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly ICacheManager _cacheManager;
        private readonly ILogger<WishlistQueryService> _logger;
        private readonly IWishlistCacheHelper _cacheHelper;
        private readonly IBackgroundJobClient _backgroundJobClient;

        public WishlistQueryService(
            IUnitOfWork unitOfWork, 
            ICacheManager cacheManager, 
            ILogger<WishlistQueryService> logger,
            IWishlistCacheHelper cacheHelper,
            IBackgroundJobClient backgroundJobClient)
        {
            _unitOfWork = unitOfWork;
            _cacheManager = cacheManager;
            _logger = logger;
            _cacheHelper = cacheHelper;
            _backgroundJobClient = backgroundJobClient;
        }

        public async Task<Result<List<WishlistItemDto>>> GetWishlistAsync(string? userId, int page = 1, int pageSize = 20)
        {
            var cacheKey = _cacheHelper.GetWishlistCacheKey(userId, page, pageSize);
            var cached = await _cacheManager.GetAsync<List<WishlistItemDto>>(cacheKey);
            if (cached != null)
            {
                _logger.LogInformation("Cache hit for wishlist of user {UserId} page {Page}", userId, page);
                return Result<List<WishlistItemDto>>.Ok(cached, "Wishlist retrieved from cache", 200);
            }

            try
            {
                var query = _unitOfWork.Repository<WishlistItem>()
                    .GetAll();

                if (userId != null)
                    query = query.Where(w => w.CustomerId == userId);
                  var list=await     query
                    .Select(w => new WishlistItemDto
                    {
                        Id = w.Id,
                        CreatedAt = null,
                        ModifiedAt = null,
                        ProductId = w.ProductId,
                        UserId = w.CustomerId,
                        Product = new ProductDto
                        {
                            Id = w.Product.Id,
                            Name = w.Product.Name,
                            Price = w.Product.Price,
                            IsActive = w.Product.IsActive,
                            DiscountPrecentage = (w.Product.Discount != null && w.Product.Discount.IsActive && (w.Product.Discount.DeletedAt == null) && (w.Product.Discount.EndDate > DateTime.UtcNow)) ? w.Product.Discount.DiscountPercent : 0,
                            FinalPrice = (w.Product.Discount != null && w.Product.Discount.IsActive && (w.Product.Discount.DeletedAt == null) && (w.Product.Discount.EndDate > DateTime.UtcNow)) ? Math.Round(w.Product.Price - (((w.Product.Discount.DiscountPercent) / 100) * w.Product.Price)) : w.Product.Price,
                            images = w.Product.Images.Where(img => img.DeletedAt == null).Select(img => new ImageDto
                            {
                                Url = img.Url,
                                IsMain = img.IsMain
                                ,Id= img.Id
                            })
                        }
                    }).Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToListAsync(); ;

                
                   

                _backgroundJobClient.Enqueue(() => _cacheHelper.CacheWishlistAsync(cacheKey, list));

                return Result<List<WishlistItemDto>>.Ok(list, "Wishlist retrieved successfully", 200);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving wishlist for user {UserId}", userId);
                return Result<List<WishlistItemDto>>.Fail("An error occurred while retrieving wishlist", 500);
            }
        }

        public async Task<Result<bool>> IsInWishlistAsync(string userId, int productId)
        {
            try
            {
                var exists = await _unitOfWork.Repository<WishlistItem>()
                    .GetAll()
                    .AnyAsync(w => w.CustomerId == userId && w.ProductId == productId);

                return Result<bool>.Ok(exists, exists ? "In wishlist" : "Not in wishlist", 200);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking wishlist for user {UserId}", userId);
                return Result<bool>.Fail("Failed to check wishlist", 500);
            }
        }
    }
}


