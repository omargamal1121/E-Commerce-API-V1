using System;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;
using E_Commerce.DtoModels.ProductDtos;
using E_Commerce.DtoModels.Responses;
using E_Commerce.Interfaces;
using E_Commerce.Models;
using E_Commerce.Services.Cache;
using E_Commerce.UOW;
using Hangfire;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using E_Commerce.DtoModels.ImagesDtos;

namespace E_Commerce.Services.WishlistServices
{
    public class WishlistService : IWishlistService
    {
        private const string CACHE_TAG_WISHLIST = "wishlist";
        private readonly IUnitOfWork _unitOfWork;
        private readonly ICacheManager _cacheManager;
        private readonly ILogger<WishlistService> _logger;

        public WishlistService(IUnitOfWork unitOfWork, ICacheManager cacheManager, ILogger<WishlistService> logger)
        {
            _unitOfWork = unitOfWork;
            _cacheManager = cacheManager;
            _logger = logger;
        }

        public async Task<Result<List<WishlistItemDto>>> GetWishlistAsync(string userId, int page = 1, int pageSize = 20)
        {
            var cacheKey = $"{CACHE_TAG_WISHLIST}_{userId}_page_{page}_size_{pageSize}";
            var cached = await _cacheManager.GetAsync<List<WishlistItemDto>>(cacheKey);
            if (cached != null)
            {
                _logger.LogInformation("Cache hit for wishlist of user {UserId} page {Page}", userId, page);
                return Result<List<WishlistItemDto>>.Ok(cached, "Wishlist retrieved from cache", 200);
            }

            try
            {
                var query = _unitOfWork.Repository<WishlistItem>()
                    .GetAll()
                    .Where(w => w.CustomerId == userId)
                    .OrderByDescending(w => w.Id)
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
                            images = w.Product.Images.Where(img => img.DeletedAt == null).Select(img =>new ImageDto{
                                Url= img.Url,IsMain=img.IsMain })
                        }
                    });

                var list = await query
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToListAsync();

                BackgroundJob.Enqueue(() => CacheWishlistInBackground(cacheKey, list));

                return Result<List<WishlistItemDto>>.Ok(list, "Wishlist retrieved successfully", 200);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving wishlist for user {UserId}", userId);
                return Result<List<WishlistItemDto>>.Fail("An error occurred while retrieving wishlist", 500);
            }
        }

        public async Task<Result<bool>> AddAsync(string userId, int productId)
        {
            try
            {
                var productExists = await _unitOfWork.Repository<Product>()
                    .GetAll()
                    .AnyAsync(p => p.Id == productId && p.DeletedAt == null && p.IsActive);

                if (!productExists)
                {
                    return Result<bool>.Fail("Product not found or inactive", 404);
                }

                var exists = await _unitOfWork.Repository<WishlistItem>()
                    .GetAll()
                    .AnyAsync(w => w.CustomerId == userId && w.ProductId == productId);

                if (exists)
                {
                    return Result<bool>.Ok(true, "Already in wishlist", 200);
                }

                var entity = new WishlistItem
                {
                    CustomerId = userId,
                    ProductId = productId
                };

                await _unitOfWork.Repository<WishlistItem>().CreateAsync(entity);
                await _unitOfWork.CommitAsync();

                await InvalidateWishlistCache(userId);
                return Result<bool>.Ok(true, "Added to wishlist", 201);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding product {ProductId} to wishlist for user {UserId}", productId, userId);
                return Result<bool>.Fail("Failed to add to wishlist", 500);
            }
        }

        public async Task<Result<bool>> RemoveAsync(string userId, int productId)
        {
            try
            {
                var repo = _unitOfWork.Repository<WishlistItem>();
                var item = await repo.GetAll()
                    .FirstOrDefaultAsync(w => w.CustomerId == userId && w.ProductId == productId);

                if (item == null)
                {
                    return Result<bool>.Ok(true, "Item not in wishlist", 200);
                }

                repo.Remove(item);
                await _unitOfWork.CommitAsync();

                await InvalidateWishlistCache(userId);
                return Result<bool>.Ok(true, "Removed from wishlist", 200);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error removing product {ProductId} from wishlist for user {UserId}", productId, userId);
                return Result<bool>.Fail("Failed to remove from wishlist", 500);
            }
        }

        public async Task<Result<bool>> ClearAsync(string userId)
        {
            try
            {
                var repo = _unitOfWork.Repository<WishlistItem>();
                var items = await repo.GetAll().Where(w => w.CustomerId == userId).ToListAsync();
                if (items.Count == 0)
                {
                    return Result<bool>.Ok(true, "Wishlist already empty", 200);
                }

                repo.RemoveList(items);
                await _unitOfWork.CommitAsync();

                await InvalidateWishlistCache(userId);
                return Result<bool>.Ok(true, "Wishlist cleared", 200);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error clearing wishlist for user {UserId}", userId);
                return Result<bool>.Fail("Failed to clear wishlist", 500);
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

        // Background cache set for Hangfire
        public async Task CacheWishlistInBackground(string cacheKey, List<WishlistItemDto> items)
        {
            try
            {
                await _cacheManager.SetAsync(cacheKey, items, TimeSpan.FromMinutes(10));
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to cache wishlist for key {CacheKey}", cacheKey);
            }
        }

        private async Task InvalidateWishlistCache(string userId)
        {
            try
            {
                await _cacheManager.RemoveByTagAsync(CACHE_TAG_WISHLIST + "_" + userId);
            }
            catch
            {
                // Best-effort; ignore failures
            }
        }
    }
}
