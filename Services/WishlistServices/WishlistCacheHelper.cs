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
        private const string CACHE_KEY_WishlistIds = "Ids";
        private  string[] tags= new string[] { CACHE_TAG_WISHLIST };
        private readonly ICacheManager _cacheManager;
        private readonly ILogger<WishlistCacheHelper> _logger;

        public WishlistCacheHelper(
            ICacheManager cacheManager,
            ILogger<WishlistCacheHelper> logger)
        {
            _cacheManager = cacheManager;
            _logger = logger;
        }

        private string GetWishlistCacheKey(string userId, int page, int pageSize,bool isadmin,bool all)
        {
            return $"{CACHE_TAG_WISHLIST}_{userId}_page_{page}_size_{pageSize}_isadmin_{isadmin}_all_{all}";
        }
        private string GetWishlistIdsCacheKey(string userId)
        {
            return $"{CACHE_KEY_WishlistIds}_{userId}";
        }

        public async Task CacheWishlistAsync(string userId, int page, int pageSize, bool isadmin, bool all, List<WishlistItemDto> items)
        {
            string cacheKey = GetWishlistCacheKey(userId, page,pageSize,isadmin,all); 
            try
            {
                await _cacheManager.SetAsync(cacheKey, items, TimeSpan.FromMinutes(10),tags:tags);
                _logger.LogInformation("Successfully cached wishlist for key {CacheKey}", cacheKey);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to cache wishlist for key {CacheKey}", cacheKey);
            }
        }

        public async Task CacheWishlistIdsAsync(string userid, HashSet<int> items)
        {
            try
            {
                await _cacheManager.SetAsync(userid, items, TimeSpan.FromMinutes(10),tags:tags);
                _logger.LogInformation("Successfully cached wishlist for key {CacheKey}", userid);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to cache wishlist for key {CacheKey}", userid);
            }
        }
        public async Task<List<WishlistItemDto>?> GetCachedWishlistAsync(string userId, int page, int pageSize, bool isadmin, bool all)
        {
                var cacheKey = GetWishlistCacheKey(userId, page, pageSize, isadmin, all);
            try
            {
                var cachedItems = await _cacheManager.GetAsync<List<WishlistItemDto>>(cacheKey);
                if (cachedItems != null)
                {
                    _logger.LogInformation("Cache hit for wishlist with key {CacheKey}", cacheKey);
                    return cachedItems;
                }
                else
                {
                    _logger.LogInformation("Cache miss for wishlist with key {CacheKey}", cacheKey);
                    return null;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to retrieve cached wishlist for key {CacheKey}", cacheKey);
                return null;
            }
        }
        public async Task<HashSet<int>?> GetCachedWishlistidsAsync(string userId)
        {
                var cacheKey = GetWishlistIdsCacheKey(userId);
            try
            {
                var cachedItems = await _cacheManager.GetAsync<HashSet<int>>(cacheKey);
                if (cachedItems != null)
                {
                    _logger.LogInformation("Cache hit for wishlist with key {CacheKey}", cacheKey);
                    return cachedItems;
                }
                else
                {
                    _logger.LogInformation("Cache miss for wishlist with key {CacheKey}", cacheKey);
                    return null;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to retrieve cached wishlist for key {CacheKey}", cacheKey);
                return null;
            }
        }

        public async Task InvalidateWishlistCacheAsync(string userId)
        {
            try
            {
                await _cacheManager.RemoveByTagsAsync(tags);
                _logger.LogInformation("Successfully invalidated wishlist cache for user {UserId}", userId);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to invalidate wishlist cache for user {UserId}", userId);
            }
        }
    }
}
