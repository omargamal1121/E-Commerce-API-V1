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
    public class WishlistQueryService : IWishlistQueryService
    {
        private readonly IUnitOfWork _unitOfWork;

        private readonly ILogger<WishlistQueryService> _logger;
        private readonly IWishlistCacheHelper _cacheHelper;
        private readonly IBackgroundJobClient _backgroundJobClient;
        private readonly IWishListMapper _wishListMapper;

        public WishlistQueryService(
            IWishListMapper wishListMapper,
            IUnitOfWork unitOfWork,
            ILogger<WishlistQueryService> logger,
            IWishlistCacheHelper cacheHelper,
            IBackgroundJobClient backgroundJobClient)
        {
            _wishListMapper = wishListMapper;
            _unitOfWork = unitOfWork;

            _logger = logger;
            _cacheHelper = cacheHelper;
            _backgroundJobClient = backgroundJobClient;
        }

        public async Task<Result<List<WishlistItemDto>>> GetWishlistAsync(
            string userId, bool all = false, int page = 1, int pageSize = 20, bool isadmin = false)
        {
           

            _logger.LogInformation("Starting to retrieve wishlist for user {UserId}. Page: {Page}, PageSize: {PageSize}, All: {All}, IsAdmin: {IsAdmin}",
                userId, page, pageSize, all, isadmin);

            // Try get from cache first
            var cached = await _cacheHelper.GetCachedWishlistAsync(userId,page,pageSize,isadmin,all);
            if (cached != null)
            {
                _logger.LogInformation("✅ Cache hit for wishlist of user {UserId} (Page {Page})", userId, page);
                return Result<List<WishlistItemDto>>.Ok(cached, "Wishlist retrieved from cache", 200);
            }

            _logger.LogInformation("❌ Cache miss for wishlist of user {UserId}. Fetching from database...", userId);

            try
            {
                var query = _unitOfWork.Repository<WishlistItem>().GetAll().AsNoTracking();

                if(!all && userId==null)
                {
                    _logger.LogWarning("UserId is null while 'all' is false. Cannot proceed.");
                    return Result<List<WishlistItemDto>>.Fail("Login frist", 401);
                }
                // Filtering by user
                if (!all)
                {
                    query = query.Where(w => w.CustomerId == userId);
                    _logger.LogDebug("Filtering wishlist for user {UserId}", userId);
                }

                // Filter out inactive/deleted products for normal users
                if (!isadmin)
                {
                    query = query.Where(w => w.Product.IsActive && w.Product.Quantity > 0 && w.Product.DeletedAt == null);
                    _logger.LogDebug("Applying active/available product filters (UserId: {UserId})", userId);
                }

                // Pagination
                query = query.Skip((page - 1) * pageSize).Take(pageSize);
                _logger.LogDebug("Applying pagination. Page: {Page}, PageSize: {PageSize}", page, pageSize);

                // Mapping
                var list = _wishListMapper.MapToWishlistItemDto(query).ToList();
                _logger.LogInformation("Wishlist items mapped successfully for user {UserId}. Count: {Count}", userId, list.Count);

                // Cache asynchronously
                _backgroundJobClient.Enqueue(() => _cacheHelper.CacheWishlistAsync(userId,page,pageSize,isadmin,all, list));
                _logger.LogInformation("Enqueued background job to cache wishlist for user {UserId}", userId);

                _logger.LogInformation("✅ Wishlist retrieved successfully for user {UserId}", userId);
                return Result<List<WishlistItemDto>>.Ok(list, "Wishlist retrieved successfully", 200);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error retrieving wishlist for user {UserId}", userId);
                return Result<List<WishlistItemDto>>.Fail("An error occurred while retrieving wishlist", 500);
            }
        }

        public async Task<Result<bool>> IsInWishlistAsync(string userId, int productId)
        {
            _logger.LogInformation("Checking if product {ProductId} is in wishlist for user {UserId}", productId, userId);

            try
            {
                var exists = await _unitOfWork.Repository<WishlistItem>()
                    .GetAll()
                    .AnyAsync(w => w.CustomerId == userId && w.ProductId == productId);

                _logger.LogInformation("Product {ProductId} {Status} in wishlist for user {UserId}",
                    productId, exists ? "exists" : "does not exist", userId);

                return Result<bool>.Ok(exists, exists ? "In wishlist" : "Not in wishlist", 200);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error checking wishlist for user {UserId} and product {ProductId}", userId, productId);
                return Result<bool>.Fail("Failed to check wishlist", 500);
            }
        }
        public async Task<HashSet<int>> GetUserWishlistProductIdsAsync(string userId)
        {
           
            var cached = await _cacheHelper.GetCachedWishlistidsAsync(userId);
            if (cached != null)
                return cached;

            var ids = await _unitOfWork.Repository<WishlistItem>()
                .GetAll()
                .Where(w => w.CustomerId == userId)
                .Select(w => w.ProductId)
                .ToListAsync();

            var result = new HashSet<int>(ids);
            _backgroundJobClient.Enqueue(()=> _cacheHelper.CacheWishlistIdsAsync(userId, result));
            return result;
        }

    }
}
