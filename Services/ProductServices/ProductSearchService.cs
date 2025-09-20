using E_Commerce.DtoModels.CategoryDtos;
using E_Commerce.DtoModels.CollectionDtos;
using E_Commerce.DtoModels.DiscoutDtos;
using E_Commerce.DtoModels.ImagesDtos;
using E_Commerce.DtoModels.ProductDtos;
using E_Commerce.DtoModels.Responses;
using E_Commerce.Enums;
using E_Commerce.ErrorHnadling;
using E_Commerce.Interfaces;
using E_Commerce.Models;
using E_Commerce.Services.EmailServices;
using E_Commerce.UOW;
using Hangfire;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using System.Linq.Expressions;

namespace E_Commerce.Services.ProductServices
{
    public interface IProductSearchService
    {
        public Task<Result<List<BestSellingProductDto>>> GetBestSellerProductsWithCountAsync(bool? isDeleted, bool? isActive, int page = 1, int pagesize = 10, bool IsAdmin = false);
        Task<Result<List<ProductDto>>> GetNewArrivalsAsync(int page, int pageSize, bool? isActive = null, bool? deletedOnly = null, bool IsAdmin = false);
        Task<Result<List<ProductDto>>> GetBestSellersAsync(int page, int pageSize, bool? isActive = null, bool? deletedOnly = null, bool IsAdmin = false);
        Task<Result<List<ProductDto>>> AdvancedSearchAsync(AdvancedSearchDto searchCriteria, int page, int pageSize, bool? isActive = null, bool? deletedOnly = null, bool IsAdmin = false);
    }

    public class ProductSearchService : IProductSearchService
    {
        private readonly IBackgroundJobClient _backgroundJobClient;
        private readonly IproductMapper _productMapper;
        private readonly IProductCacheManger _productCacheManger;
        private readonly IUnitOfWork _unitOfWork;
        private readonly ILogger<ProductSearchService> _logger;
        private readonly IErrorNotificationService _errorNotificationService;

        public ProductSearchService(
            IproductMapper iproductMapper,
            IProductCacheManger productCacheManger,
            IBackgroundJobClient backgroundJobClient,
            IUnitOfWork unitOfWork,
            ILogger<ProductSearchService> logger,
            IErrorNotificationService errorNotificationService
        )
        {
            _productCacheManger = productCacheManger;
            _productMapper = iproductMapper;
            _backgroundJobClient = backgroundJobClient;
            _unitOfWork = unitOfWork;
            _logger = logger;
            _errorNotificationService = errorNotificationService;
        }

        private IQueryable<E_Commerce.Models.Product> BasicFilter(IQueryable<E_Commerce.Models.Product> query, bool? isActive, bool? DeletedOnly, bool IsAdmin = false)
        {
            if (!IsAdmin)
            {
                isActive = true;
                DeletedOnly = false;
            }
            if (isActive.HasValue)
            {
                if (isActive.Value)
                    query = query.Where(p => p.IsActive);
                else
                    query = query.Where(p => !p.IsActive);
            }
            if (DeletedOnly.HasValue)
            {
                if (DeletedOnly.Value)
                    query = query.Where(p => p.DeletedAt != null);
                else
                    query = query.Where(p => p.DeletedAt == null);
            }
            return query;
        }

    
        public async Task<Result<List<ProductDto>>> GetNewArrivalsAsync(int page, int pageSize, bool? isActive = null, bool? deletedOnly = null, bool IsAdmin = false)
        {
            if (page <= 0 || pageSize <= 0)
                return Result<List<ProductDto>>.Fail("Invalid page or pageSize. Must be greater than 0.", 400);

            // For non-admin users, restrict to active and non-deleted products
            if (!IsAdmin)
            {
                isActive = true;
                deletedOnly = false;
            }

          
            var cached = await _productCacheManger.GetProductListCacheAsync<List<ProductDto>>(
                null, isActive, deletedOnly, pageSize, page, "NewArrivals", IsAdmin);
                
            if (cached != null)
                return Result<List<ProductDto>>.Ok(cached, $"Found {cached.Count} new arrivals", 200);

            try
            {
                var thirtyDaysAgo = DateTime.UtcNow.AddDays(-90);
                var query = _unitOfWork.Product.GetAll().Where(p => p.CreatedAt >= thirtyDaysAgo);

                query = BasicFilter(query, isActive, deletedOnly,IsAdmin);

                var products = await _productMapper.maptoProductDtoexpression(query, IsAdmin)
                    .OrderByDescending(p => p.CreatedAt)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToListAsync();

                if (!products.Any())
                    return Result<List<ProductDto>>.Fail("No new arrivals found", 404);

                _backgroundJobClient.Enqueue(() => _productCacheManger.SetProductListCacheAsync(products, null, isActive, deletedOnly, pageSize, page, "NewArrial", IsAdmin));
                return Result<List<ProductDto>>.Ok(products, $"Found {products.Count} new arrivals", 200);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in GetNewArrivalsAsync");
                _backgroundJobClient.Enqueue(() => _errorNotificationService.SendErrorNotificationAsync(ex.Message, ex.StackTrace));
                return Result<List<ProductDto>>.Fail("Error retrieving new arrivals", 500);
            }
        }

        public async Task<Result<List<ProductDto>>> GetBestSellersAsync(int page, int pageSize, bool? isActive = null, bool? deletedOnly = null, bool IsAdmin = false)
        {
            if (page <= 0 || pageSize <= 0)
                return Result<List<ProductDto>>.Fail("Invalid page or pageSize. Must be greater than 0.", 400);
            
            if (!IsAdmin)
            {
                isActive = true;
                deletedOnly = false;
            }
            
            var cached = await _productCacheManger.GetProductListCacheAsync<List<ProductDto>>(
                null, isActive, deletedOnly, pageSize, page, "BestSeller", IsAdmin);
                
            if (cached != null)
                return Result<List<ProductDto>>.Ok(cached, $"Found {cached.Count} best sellers", 200);

            try
            {
                var bestSellerQuery = _unitOfWork.Repository<OrderItem>().GetAll()
                    .Where(i => i.Order.Status != OrderStatus.CancelledByAdmin && i.Order.Status != OrderStatus.CancelledByUser)
                    .GroupBy(i => i.ProductId)
                    .Select(g => new
                    {
                        ProductId = g.Key,
                        TotalQuantity = g.Sum(x => x.Quantity)
                    })
                    .OrderByDescending(g => g.TotalQuantity);

                var productQuery = bestSellerQuery
                    .Join(_unitOfWork.Product.GetAll().Include(p => p.Images),
                          g => g.ProductId,
                          p => p.Id,
                          (g, p) => p)
                    .AsQueryable();

                productQuery = BasicFilter(productQuery, isActive, deletedOnly,IsAdmin);

                var products = await _productMapper.maptoProductDtoexpression(productQuery, IsAdmin)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToListAsync();

                if (!products.Any())
                {
                    var query =  _unitOfWork.Product.GetAll()
                        .Where(p => isActive == null || p.IsActive == isActive)
                        .OrderBy(r => Guid.NewGuid())
                        .Take(pageSize);
                    var fallbackProducts = await
                       _productMapper.maptoProductDtoexpression(query, IsAdmin)
                        .ToListAsync();

                    return Result<List<ProductDto>>.Ok(fallbackProducts, "No best sellers found. Showing random products instead.", 200);
                }

                var result = Result<List<ProductDto>>.Ok(products, $"Found {products.Count} best sellers", 200);

                _backgroundJobClient.Enqueue(() => _productCacheManger.SetProductListCacheAsync(products,null, isActive, deletedOnly,pageSize,page,"BestSeller", IsAdmin));

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in GetBestSellersAsync");
                _backgroundJobClient.Enqueue(() => _errorNotificationService.SendErrorNotificationAsync(ex.Message, ex.StackTrace));
                return Result<List<ProductDto>>.Fail("Error retrieving best sellers", 500);
            }
        }

        public async Task<Result<List<ProductDto>>> AdvancedSearchAsync(AdvancedSearchDto searchCriteria, int page, int pageSize, bool? isActive = null, bool? deletedOnly = null, bool IsAdmin = false)
        {
            if (page <= 0 || pageSize <= 0)
                return Result<List<ProductDto>>.Fail("Invalid page or pageSize. Must be greater than 0.", 400);
            try
            {
                var serializedCriteria = JsonConvert.SerializeObject(searchCriteria);
                string searchKey = $"advsearch_{serializedCriteria}_{page}_{pageSize}_{isActive}_{deletedOnly}";

  
                if (!IsAdmin)
                {
                    isActive = true;
                    deletedOnly = false;
                }


                var cached = await _productCacheManger.GetProductListCacheAsync<List<ProductDto>>(
                    searchKey, isActive, deletedOnly, pageSize, page, "AdvancedSearch", IsAdmin);
                
                if (cached != null)
                    return Result<List<ProductDto>>.Ok(cached, $"Found {cached.Count} products matching your search", 200);

                var query = _unitOfWork.Product.GetAll();
                query = BasicFilter(query, isActive, deletedOnly,IsAdmin);

                // Apply filters
                if (searchCriteria.Subcategoryid.HasValue)
                    query = query.Where(p => p.SubCategoryId == searchCriteria.Subcategoryid.Value);

                if (searchCriteria.Gender.HasValue)
                    query = query.Where(p => p.Gender == searchCriteria.Gender.Value);

                if (searchCriteria.FitType.HasValue)
                    query = query.Where(p => p.fitType == (FitType)searchCriteria.FitType.Value);

                if (searchCriteria.InStock.HasValue)
                {
                    query = searchCriteria.InStock.Value
                        ? query.Where(p => p.Quantity > 0)
                        : query.Where(p => p.Quantity == 0);
                }

                if (!string.IsNullOrWhiteSpace(searchCriteria.SearchTerm))
                {
                    query = query.Where(p =>
                        p.Name.Contains(searchCriteria.SearchTerm) ||
                        p.Description.Contains(searchCriteria.SearchTerm));
                }

                if (searchCriteria.OnSale.HasValue && searchCriteria.OnSale.Value)
                {
                    query = query.Where(p => p.Discount != null &&
                        p.Discount.IsActive &&
                        p.Discount.DeletedAt == null &&
                        p.Discount.StartDate <= DateTime.UtcNow &&
                        p.Discount.EndDate >= DateTime.UtcNow);
                }

                // Apply price filtering BEFORE ToListAsync
                if (searchCriteria.MinPrice.HasValue)
                {
                    var min = searchCriteria.MinPrice.Value;
                    query = query.Where(p =>
                        p.Price >= min ||
                        (p.Discount != null && p.Discount.IsActive &&
                         p.Discount.DeletedAt == null &&
                         p.Discount.StartDate <= DateTime.UtcNow &&
                         p.Discount.EndDate >= DateTime.UtcNow &&
                         (p.Price - ((p.Discount.DiscountPercent / 100m) * p.Price)) >= min));
                }

                if (searchCriteria.MaxPrice.HasValue)
                {
                    var max = searchCriteria.MaxPrice.Value;
                    query = query.Where(p =>
                        p.Price <= max ||
                        (p.Discount != null && p.Discount.IsActive &&
                         p.Discount.DeletedAt == null &&
                         p.Discount.StartDate <= DateTime.UtcNow &&
                         p.Discount.EndDate >= DateTime.UtcNow &&
                         (p.Price - ((p.Discount.DiscountPercent / 100m) * p.Price)) <= max));
                }

                // Sorting
                query = searchCriteria.SortBy?.ToLower() switch
                {
                    "name" => searchCriteria.SortDescending ? query.OrderByDescending(p => p.Name) : query.OrderBy(p => p.Name),
                    "price" => searchCriteria.SortDescending ? query.OrderByDescending(p => p.Price) : query.OrderBy(p => p.Price),
                    "newest" => searchCriteria.SortDescending ? query.OrderByDescending(p => p.CreatedAt) : query.OrderBy(p => p.CreatedAt),
                    _ => query.OrderBy(p => p.Name)
                };
                query = query
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize);


                var products = await  _productMapper.maptoProductDtoexpression(query,IsAdmin)
                    .ToListAsync();

                if (!products.Any())
                    return Result<List<ProductDto>>.Fail("No products found matching the search criteria", 404);

                _backgroundJobClient.Enqueue(() => _productCacheManger.SetProductListCacheAsync(products, searchKey, isActive, deletedOnly));

                return Result<List<ProductDto>>.Ok(products, $"Found {products.Count} products matching search criteria", 200);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in AdvancedSearchAsync");
                _backgroundJobClient.Enqueue(() => _errorNotificationService.SendErrorNotificationAsync(ex.Message, ex.StackTrace));
                return Result<List<ProductDto>>.Fail("Error performing advanced search", 500);
            }
        }

        public async Task<Result<List<BestSellingProductDto>>> GetBestSellerProductsWithCountAsync(bool? isDeleted, bool? isActive, int page = 1, int pagesize = 10,bool IsAdmin=false)
        {

            var cachedResult = await _productCacheManger.GetProductListCacheAsync<List<BestSellingProductDto>>(null, isActive, isDeleted, pagesize,page,"BestSeller",IsAdmin);
            if (cachedResult is not null)
                return Result<List<BestSellingProductDto>>.Ok(cachedResult);

            var productsQuery = _unitOfWork.Product.GetAll()
                .Include(p => p.ProductVariants)
                    .ThenInclude(v => v.OrderItems)
                .Include(p => p.Images)
                .AsQueryable();

            productsQuery = BasicFilter(productsQuery, isActive, isDeleted,IsAdmin);

            var productSales = await productsQuery
                .Select(p => new
                {
                    Product = p,
                    TotalSold = p.ProductVariants
                        .SelectMany(v => v.OrderItems)
                        .Where(oi => oi.Order.Status == OrderStatus.Confirmed)
                        .Sum(oi => (int?)oi.Quantity) ?? 0,
                         ImageUrl = p.Images
                        .Where(i => i.IsMain && i.DeletedAt == null)
                        .Select(i => i.Url)
                        .FirstOrDefault()
                })
                .Where(p => p.TotalSold > 0)
                .OrderByDescending(p => p.TotalSold)
                .Skip((page - 1) * pagesize)
                    .Take(pagesize)
                .ToListAsync();

            var bestSellers = productSales.Select(p => new BestSellingProductDto
            {
                ProductId = p.Product.Id,
                ProductName = p.Product.Name,
                Image = p.ImageUrl,
                TotalSoldQuantity = p.TotalSold
            }).ToList();

            _backgroundJobClient.Enqueue(() =>
                _productCacheManger.SetProductListCacheAsync(bestSellers, null, isActive, isDeleted,pagesize,page,"BestSeller"));

            return Result<List<BestSellingProductDto>>.Ok(bestSellers);
        }
    }
}
