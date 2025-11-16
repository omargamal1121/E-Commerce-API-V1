using System;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;
using ApplicationLayer.DtoModels.ProductDtos;
using ApplicationLayer.DtoModels.Responses;
using ApplicationLayer.Interfaces;
using DomainLayer.Models;
using ApplicationLayer.Services.Cache;


namespace ApplicationLayer.Services.WishlistServices
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
        public async Task<Result<List<WishlistItemDto>>> GetWishlistAsync(string userId, int page = 1, int pageSize = 20)
            => await _queryService.GetWishlistAsync(userId, page, pageSize);

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


