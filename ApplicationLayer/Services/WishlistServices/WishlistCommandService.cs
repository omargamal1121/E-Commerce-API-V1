using System;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;
using ApplicationLayer.DtoModels.Responses;
using ApplicationLayer.Interfaces;
using DomainLayer.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
namespace ApplicationLayer.Services.WishlistServices
{
    public class WishlistCommandService : IWishlistCommandService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly ILogger<WishlistCommandService> _logger;
        private readonly IWishlistCacheHelper _cacheHelper;

        public WishlistCommandService(
            IUnitOfWork unitOfWork,
            ILogger<WishlistCommandService> logger,
            IWishlistCacheHelper cacheHelper)
        {
            _unitOfWork = unitOfWork;
            _logger = logger;
            _cacheHelper = cacheHelper;
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

                await _cacheHelper.InvalidateWishlistCacheAsync(userId);
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

                await _cacheHelper.InvalidateWishlistCacheAsync(userId);
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

                await _cacheHelper.InvalidateWishlistCacheAsync(userId);
                return Result<bool>.Ok(true, "Wishlist cleared", 200);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error clearing wishlist for user {UserId}", userId);
                return Result<bool>.Fail("Failed to clear wishlist", 500);
            }
        }
    }
}


