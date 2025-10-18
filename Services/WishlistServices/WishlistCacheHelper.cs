using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using E_Commerce.DtoModels.ProductDtos;
using E_Commerce.Services.Cache;
using Microsoft.Extensions.Logging;

namespace E_Commerce.Services.WishlistServices
{
    public class WishlistCacheHelper : IWishlistCacheHelper
    {
        private const string CACHE_TAG_WISHLIST = "wishlist";
        private readonly ICacheManager _cacheManager;
        private readonly ILogger<WishlistCacheHelper> _logger;

        public WishlistCacheHelper(
            ICacheManager cacheManager,
            ILogger<WishlistCacheHelper> logger)
        {
            _cacheManager = cacheManager;
            _logger = logger;
        }

        public string GetWishlistCacheKey(string userId, int page, int pageSize)
        {
            return $"{CACHE_TAG_WISHLIST}_{userId}_page_{page}_size_{pageSize}";
        }

        public async Task CacheWishlistAsync(string cacheKey, List<WishlistItemDto> items)
        {
            try
            {
                await _cacheManager.SetAsync(cacheKey, items, TimeSpan.FromMinutes(10));
                _logger.LogInformation("Successfully cached wishlist for key {CacheKey}", cacheKey);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to cache wishlist for key {CacheKey}", cacheKey);
            }
        }

        public async Task InvalidateWishlistCacheAsync(string userId)
        {
            try
            {
                await _cacheManager.RemoveByTagAsync(CACHE_TAG_WISHLIST + "_" + userId);
                _logger.LogInformation("Successfully invalidated wishlist cache for user {UserId}", userId);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to invalidate wishlist cache for user {UserId}", userId);
            }
        }
    }
}
