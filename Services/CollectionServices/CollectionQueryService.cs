using E_Commerce.DtoModels.CollectionDtos;
using E_Commerce.DtoModels.Responses;
using E_Commerce.ErrorHnadling;
using E_Commerce.Interfaces;
using E_Commerce.Services.EmailServices;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;

namespace E_Commerce.Services.Collection
{
    public class CollectionQueryService : ICollectionQueryService
    {
        private readonly ILogger<CollectionQueryService> _logger;
        private readonly ICollectionRepository _collectionRepository;
        private readonly IErrorNotificationService _errorNotificationService;
        private readonly ICollectionCacheHelper _cacheHelper;
        private readonly ICollectionMapper _mapper;

        public CollectionQueryService(
            ILogger<CollectionQueryService> logger,
            ICollectionRepository collectionRepository,
            IErrorNotificationService errorNotificationService,
            ICollectionCacheHelper cacheHelper,
            ICollectionMapper mapper)
        {
            _logger = logger;
            _collectionRepository = collectionRepository;
            _errorNotificationService = errorNotificationService;
            _cacheHelper = cacheHelper;
            _mapper = mapper;
        }

        private IQueryable<E_Commerce.Models.Collection> BasicFilter(IQueryable<E_Commerce.Models.Collection> query, bool? IsActive = null, bool? IsDeleted = null, bool IsAdmin = false)
        {
            if (!IsAdmin)
            {
                return query.Where(q => q.IsActive && q.DeletedAt == null);
            }
            if (IsActive.HasValue)
                query = query.Where(x => x.IsActive == IsActive.Value);
            if (IsDeleted.HasValue)
            {
                if (IsDeleted.Value)
                    query = query.Where(q => q.DeletedAt != null);
                else
                    query = query.Where(q => q.DeletedAt == null);
            }
            return query;
        }

        public async Task<Result<CollectionDto>> GetCollectionByIdAsync(int collectionId, bool? IsActive = null, bool? IsDeleted = null, bool IsAdmin = false)
        {
            _logger.LogInformation($"Getting collection by ID: {collectionId}");

            var cached = await _cacheHelper.GetCollectionByIdCacheAsync<CollectionDto>(collectionId, IsActive, IsDeleted, IsAdmin);
            if (cached != null)
            {
                _logger.LogInformation($"Cache hit for collection {collectionId}");
                return Result<CollectionDto>.Ok(cached, "Collection retrieved from cache", 200);
            }

            try
            {
                var query = _collectionRepository.GetAll().Where(c => c.Id == collectionId);
                if (!IsAdmin)
                {
                    IsActive = true;
                    IsDeleted = false;
                }
                query = BasicFilter(query, IsActive, IsDeleted, IsAdmin);
                var collectionDto = await _mapper.CollectionSelectorWithData(query, IsAdmin).FirstOrDefaultAsync();
                if (collectionDto == null)
                {
                    _logger.LogInformation($"Collection with ID {collectionId} not found");
                    return Result<CollectionDto>.Fail("Collection not found", 404);
                }

                 _cacheHelper.SetCollectionByIdCacheAsync(collectionId, IsActive, IsDeleted, collectionDto, IsAdmin);

                return Result<CollectionDto>.Ok(collectionDto, "Collection retrieved successfully", 200);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error getting collection {collectionId}: {ex.Message}");
                _cacheHelper.NotifyAdminError($"Error getting collection {collectionId}: {ex.Message}", ex.StackTrace);
                return Result<CollectionDto>.Fail("An error occurred while retrieving collection", 500);
            }
        }

        public async Task<Result<List<CollectionSummaryDto>>> SearchCollectionsAsync(string? searchTerm, bool? IsActive = null, bool? IsDeleted = null, int page = 1, int pagesize = 10, bool IsAdmin = false)
        {
            _logger.LogInformation($"Searching collections with term: {searchTerm}");

            try
            {
                var cached = await _cacheHelper.GetCollectionListCacheAsync<List<CollectionSummaryDto>>(searchTerm, IsActive, IsDeleted, page, pagesize, IsAdmin);
                if (cached != null)
                {
                    _logger.LogInformation("Cache hit for collection list search");
                    return Result<List<CollectionSummaryDto>>.Ok(cached, "Collections search completed successfully (cache)", 200);
                }

                var query = _collectionRepository.GetCollectionsByName(searchTerm, IsActive, IsDeleted);
                query = BasicFilter(query, IsActive, IsDeleted, IsAdmin);
                var collectionDtos = await _mapper.CollectionSelector(query)
                    .OrderBy(x => x.DisplayOrder)
                    .ThenBy(x => x.CreatedAt)
                    .Skip((page - 1) * pagesize)
                    .Take(pagesize)
                    .ToListAsync();
                 _cacheHelper.SetCollectionListCacheAsync(collectionDtos, searchTerm, IsActive, IsDeleted, page, pagesize, IsAdmin);
                return Result<List<CollectionSummaryDto>>.Ok(collectionDtos, "Collections search completed successfully", 200);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error searching collections: {ex.Message}");
                _cacheHelper.NotifyAdminError($"Error searching collections: {ex.Message}", ex.StackTrace);
                return Result<List<CollectionSummaryDto>>.Fail("An error occurred while searching collections", 500);
            }
        }
    }
}
