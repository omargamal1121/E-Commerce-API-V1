using ApplicationLayer.DtoModels.ProductDtos;
using ApplicationLayer.DtoModels.Responses;
using DomainLayer.Enums;
using ApplicationLayer.ErrorHnadling;
using ApplicationLayer.Interfaces;

using Microsoft.EntityFrameworkCore;
using Hangfire;
using ApplicationLayer.Services.Cache;
using ApplicationLayer.Services.EmailServices;
using DomainLayer.Models;
using ApplicationLayer.Services.ProductVariantServices;
using Microsoft.Extensions.Logging;

namespace ApplicationLayer.Services.ProductVariantServices
{
    public class ProductVariantQueryService : IProductVariantQueryService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly ILogger<ProductVariantQueryService> _logger;
        private readonly ICacheManager _cacheManager;
        private readonly IBackgroundJobClient _backgroundJobClient;
        private readonly IProductVariantCacheHelper _cacheHelper;
        private readonly IErrorNotificationService _errorNotificationService;
        private readonly IProductVariantMapper _mapper;

        public ProductVariantQueryService(
            IUnitOfWork unitOfWork,
            ILogger<ProductVariantQueryService> logger,
            ICacheManager cacheManager,
            IBackgroundJobClient backgroundJobClient,
            IProductVariantCacheHelper cacheHelper,
            IErrorNotificationService errorNotificationService,
            IProductVariantMapper mapper)
        {
            _unitOfWork = unitOfWork;
            _logger = logger;
            _cacheManager = cacheManager;
            _backgroundJobClient = backgroundJobClient;
            _cacheHelper = cacheHelper;
            _errorNotificationService = errorNotificationService;
            _mapper = mapper;
        }

        public async Task<Result<List<ProductVariantDto>>> GetProductVariantsAsync(int id)
        {
            var cacheKey = _cacheHelper.GetProductVariantsCacheKey(id);
            var cached = await _cacheManager.GetAsync<List<ProductVariantDto>>(cacheKey);
            if (cached != null)
                return Result<List<ProductVariantDto>>.Ok(cached, "Product variants retrieved from cache", 200);

            var result = await GetProductVariantsAsync(id, null, null);
            if (result.Success)
            {
                // Cache the results using the helper
                _cacheHelper.CacheProductVariantsAsync(id, result.Data);
            }
            return result;
        }

        public async Task<Result<List<ProductVariantDto>>> GetProductVariantsAsync(int productId, bool? isActive, bool? deletedOnly)
        {
            try
            {
                var query = _unitOfWork.Repository<ProductVariant>().GetAll().Where(v => v.ProductId == productId);

                if (isActive.HasValue)
                    query = query.Where(v => v.IsActive == isActive.Value);

                if (deletedOnly.HasValue)
                {
                    if (deletedOnly.Value)
                        query = query.Where(v => v.DeletedAt != null);
                    else
                        query = query.Where(v => v.DeletedAt == null);
                }
                else
                {
                    query = query.Where(v => v.DeletedAt == null);
                }

                var variants = await query.ToListAsync();

                if (!variants.Any())
                    return Result<List<ProductVariantDto>>.Fail("No variants found for this product", 404);

                // Use mapper to convert to DTOs
                var variantDtos = _mapper.MapToProductVariantDtoList(variants);

                return Result<List<ProductVariantDto>>.Ok(variantDtos, "Product variants retrieved successfully", 200);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error in GetProductVariantsAsync for productId: {productId}");
                _backgroundJobClient.Enqueue(() => _errorNotificationService.SendErrorNotificationAsync(ex.Message, ex.StackTrace));
                return Result<List<ProductVariantDto>>.Fail("Error retrieving product variants", 500);
            }
        }

        public async Task<Result<ProductVariantDto>> GetVariantByIdAsync(int id)
        {
            _logger.LogInformation($"Getting variant by id: {id}");
            var cacheKey = _cacheHelper.GetVariantCacheKey(id);
            var cached = await _cacheManager.GetAsync<ProductVariantDto>(cacheKey);
            if (cached != null)
            {
                _logger.LogInformation($"Retrieved variant {id} from cache");
                return Result<ProductVariantDto>.Ok(cached, "Variant retrieved from cache", 200);
            }

            try
            {
                var variant = await _unitOfWork.ProductVariant.GetVariantById(id);

                if (variant == null || variant.DeletedAt != null)
                {
                    _logger.LogWarning($"Variant {id} not found or is deleted");
                    return Result<ProductVariantDto>.Fail("Variant not found", 404);
                }
                
                // Use mapper to convert to DTO
                var variantDto = _mapper.MapToProductVariantDto(variant);
                
                _logger.LogInformation($"Caching variant {id} data");
                _cacheHelper.CacheVariantAsync(id, variantDto);
                
                return Result<ProductVariantDto>.Ok(variantDto, "Variant retrieved successfully", 200);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error in GetVariantByIdAsync for id: {id}");
                _backgroundJobClient.Enqueue(() => _errorNotificationService.SendErrorNotificationAsync(ex.Message, ex.StackTrace));
                return Result<ProductVariantDto>.Fail("Error retrieving variant", 500);
            }
        }

        public async Task<Result<List<ProductVariantDto>>> GetVariantsBySearchAsync(int productId, string? color = null, int? Length = null, int? wist = null, VariantSize? size = null, bool? isActive = null, bool? deletedOnly = null)
        {
            _logger.LogInformation($"Searching variants for product {productId} with filters: color={color}, length={Length}, waist={wist}, size={size}, isActive={isActive}, deletedOnly={deletedOnly}");
            
            // Create a cache key based on search parameters
            string cacheKey = $"product:{productId}:variants:search:color:{color}:length:{Length}:waist:{wist}:size:{size}:active:{isActive}:deleted:{deletedOnly}";
            
            // Try to get from cache first
            var cached = await _cacheManager.GetAsync<List<ProductVariantDto>>(cacheKey);
            if (cached != null)
            {
                _logger.LogInformation($"Retrieved variant search results for product {productId} from cache");
                return Result<List<ProductVariantDto>>.Ok(cached, "Variants retrieved from cache", 200);
            }
            
            try
            {
                var query = _unitOfWork.ProductVariant.GetAll().Where(p => p.ProductId == productId);
                if (isActive.HasValue)
                    query = query.Where(p => p.IsActive == isActive.Value);
                if (deletedOnly.HasValue)
                {
                    if (deletedOnly.Value)
                        query = query.Where(p => p.DeletedAt != null);
                    else
                        query = query.Where(p => p.DeletedAt == null);
                }
        
                if (!string.IsNullOrEmpty(color))
                    query = query.Where(v => v.Color == color);
                if (size.HasValue)
                    query = query.Where(v => v.Size == size.Value);
                if (Length.HasValue)
                    query = query.Where(v => v.Length == Length.Value);
                if (wist.HasValue)
                    query = query.Where(v => v.Waist == wist.Value);

                var variants = await query.ToListAsync();
                    
                if (!variants.Any())
                {
                    _logger.LogWarning($"No variants found matching the search criteria for product {productId}");
                    return Result<List<ProductVariantDto>>.Fail("No variants found matching the search criteria", 404);
                }

                // Use mapper to convert to DTOs
                var variantDtos = _mapper.MapToProductVariantDtoList(variants);

                // Store results in cache using the helper
                _logger.LogInformation($"Caching search results for product {productId}");
                _cacheHelper.CacheSearchResultsAsync(productId, cacheKey, variantDtos);

                _logger.LogInformation($"Successfully retrieved {variantDtos.Count} variants for product {productId}");
                return Result<List<ProductVariantDto>>.Ok(variantDtos, "Variants retrieved successfully", 200);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error in GetVariantsBySearchAsync for productId: {productId}");
                _backgroundJobClient.Enqueue(() => _errorNotificationService.SendErrorNotificationAsync(ex.Message, ex.StackTrace));
                return Result<List<ProductVariantDto>>.Fail("Error retrieving variants by search", 500);
            }
        }
    }
}


