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
        private readonly IWishlistQueryService _queryService;
        private readonly IWishlistCommandService _commandService;

        public WishlistService(
            IWishlistQueryService queryService,
            IWishlistCommandService commandService)
        {
            _queryService = queryService;
            _commandService = commandService;
        }

        #region Query Operations (Read)
        public async Task<Result<List<WishlistItemDto>>> GetWishlistAsync(string userId,bool all=false, int page = 1, int pageSize = 20,bool isadmin=false)

            => await _queryService.GetWishlistAsync(userId,all, page, pageSize,isadmin);

        public async Task<Result<bool>> IsInWishlistAsync(string userId, int productId)
            => await _queryService.IsInWishlistAsync(userId, productId);
        #endregion

        #region Command Operations (Write)
        public async Task<Result<bool>> AddAsync(string userId, int productId)
            => await _commandService.AddAsync(userId, productId);

        public async Task<Result<bool>> RemoveAsync(string userId, int productId)
            => await _commandService.RemoveAsync(userId, productId);

        public async Task<Result<bool>> ClearAsync(string userId)
            => await _commandService.ClearAsync(userId);
        #endregion
    }
}
