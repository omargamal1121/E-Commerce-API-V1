using E_Commerce.DtoModels.CollectionDtos;
using E_Commerce.DtoModels.ImagesDtos;
using E_Commerce.DtoModels.Responses;
using E_Commerce.Interfaces;
using E_Commerce.Services.EmailServices;
using Microsoft.Extensions.Logging;

namespace E_Commerce.Services.Collection
{
    public class CollectionServices : ICollectionServices
    {
        private readonly ILogger<CollectionServices> _logger;
        private readonly ICollectionQueryService _collectionQueryService;
        private readonly ICollectionCommandService _collectionCommandService;
        private readonly ICollectionImageService _collectionImageService;
        private readonly ICollectionCacheHelper _collectionCacheHelper;
		private readonly IErrorNotificationService _errorNotificationService;

        public CollectionServices(
            ICollectionQueryService collectionQueryService,
            ICollectionCommandService collectionCommandService,
            ICollectionImageService collectionImageService,
            ICollectionCacheHelper collectionCacheHelper,
			IErrorNotificationService errorNotificationService,
            ILogger<CollectionServices> logger)
        {
            _collectionQueryService = collectionQueryService;
            _collectionCommandService = collectionCommandService;
            _collectionImageService = collectionImageService;
            _collectionCacheHelper = collectionCacheHelper;
			_errorNotificationService = errorNotificationService;
            _logger = logger;
        }
	
		public async Task CheckAndDeactivateEmptyCollectionsAsync(int productId)
		{
			await _collectionCommandService.DeactivateCollectionsWithoutActiveProductsAsync(productId);
		}

		public async Task<Result<List<ImageDto>>> AddImagesToCollectionAsync(int collectionid, List<IFormFile> images, string userId)
		{
			return await _collectionImageService.AddImagesToCollectionAsync(collectionid, images, userId);
		}

		public async Task<Result<ImageDto>> AddMainImageToCollectionAsync(int collectionid, IFormFile mainImage, string userId)
		{
			return await _collectionImageService.AddMainImageToCollectionAsync(collectionid, mainImage, userId);
		}

		public async Task<Result<bool>> RemoveImageFromCollectionAsync(int categoryId, int imageId, string userId)
		{
			return await _collectionImageService.RemoveImageFromCollectionAsync(categoryId, imageId, userId);
		}


        public async Task<Result<CollectionDto>> GetCollectionByIdAsync(int collectionId, bool? IsActive = null, bool? IsDeleted = null)
        {
            return await _collectionQueryService.GetCollectionByIdAsync(collectionId, IsActive, IsDeleted);
        }

        public async Task<Result<CollectionSummaryDto>> CreateCollectionAsync(CreateCollectionDto collectionDto, string userid)
        {
            return await _collectionCommandService.CreateCollectionAsync(collectionDto, userid);
        }

        public async Task<Result<CollectionSummaryDto>> UpdateCollectionAsync(int collectionId, UpdateCollectionDto collectionDto, string userid)
        {
            return await _collectionCommandService.UpdateCollectionAsync(collectionId, collectionDto, userid);
        }

        public async Task<Result<bool>> DeleteCollectionAsync(int collectionId, string userid)
        {
            return await _collectionCommandService.DeleteCollectionAsync(collectionId, userid);
        }

		public async Task<Result<bool>> AddProductsToCollectionAsync(int collectionId, AddProductsToCollectionDto productsDto, string userId)
		{
			return await _collectionCommandService.AddProductsToCollectionAsync(collectionId, productsDto, userId);
		}


		public async Task<Result<bool>> RemoveProductsFromCollectionAsync(int collectionId, RemoveProductsFromCollectionDto productsDto, string userId)
		{
			return await _collectionCommandService.RemoveProductsFromCollectionAsync(collectionId, productsDto, userId);
		}


		public async Task<Result<bool>> ActivateCollectionAsync(int collectionId, string userId)
		{
			return await _collectionCommandService.ActivateCollectionAsync(collectionId, userId);
		}


		public async Task<Result<bool>> DeactivateCollectionAsync(int collectionId, string userId)
		{
			return await _collectionCommandService.DeactivateCollectionAsync(collectionId, userId);
		}

		public async Task<Result<bool>> UpdateCollectionDisplayOrderAsync(int collectionId, int displayOrder, string userid)
		{
			return await _collectionCommandService.UpdateCollectionDisplayOrderAsync(collectionId, displayOrder, userid);
		}

		public async Task<Result<List<CollectionSummaryDto>>> SearchCollectionsAsync(string? searchTerm, bool? IsActive = null, bool? IsDeleted = null, int page = 1, int pagesize = 10)
        {
            return await _collectionQueryService.SearchCollectionsAsync(searchTerm, IsActive, IsDeleted, page, pagesize);
        }
    }
} 