using DomainLayer.Enums;
using ApplicationLayer.DtoModels.ImagesDtos;
using ApplicationLayer.DtoModels.ProductDtos;
using ApplicationLayer.Interfaces;
using DomainLayer.Models;
using ApplicationLayer.Services.EmailServices;

using Hangfire;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using Microsoft.Extensions.Logging;

namespace ApplicationLayer.Services.ProductServices
{

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

        private IQueryable<Product> BasicFilter(IQueryable<Product> query, bool? isActive, bool? DeletedOnly, bool IsAdmin = false)
        {
            if (!IsAdmin)
            {
                isActive = true;
                DeletedOnly = false;
                query = query.Where(p => p.Quantity > 0);
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

            if (!IsAdmin)
            {
                isActive = true;
                deletedOnly = false;
            }

            var cached = await _productCacheManger.GetProductListCacheAsync<ProductDto>(
                null, isActive, deletedOnly, pageSize, page, "NewArrivals", IsAdmin);

            if (cached != null)
                return Result<List<ProductDto>>.Ok(cached, $"Found {cached.Count} new arrivals", 200);

            try
            {
                var thirtyDaysAgo = DateTime.UtcNow.AddDays(-90);
                var query = _unitOfWork.Product.GetAll().Where(p => p.CreatedAt >= thirtyDaysAgo);

                query = BasicFilter(query, isActive, deletedOnly, IsAdmin);

                var products = await _productMapper.maptoProductDtoexpression(query, IsAdmin)
                    .OrderByDescending(p => p.CreatedAt)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToListAsync();

                if (!products.Any())
                    return Result<List<ProductDto>>.Fail("No new arrivals found", 204);

                _backgroundJobClient.Enqueue(() => _productCacheManger.SetProductListCacheAsync(products, null, isActive, deletedOnly, pageSize, page, "NewArrivals", IsAdmin, null));
                return Result<List<ProductDto>>.Ok(products, $"Found {products.Count} new arrivals", 200);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in GetNewArrivalsAsync");
                _backgroundJobClient.Enqueue(() => _errorNotificationService.SendErrorNotificationAsync(ex.Message, ex.StackTrace));
                return Result<List<ProductDto>>.Fail("Error retrieving new arrivals", 500);
            }
        }

        public async Task<Result<List<ProductDto>>> GetBestSellersAsync(
            int page,
            int pageSize,
            bool? isActive = null,
            bool? deletedOnly = null,
            bool IsAdmin = false)
        {
            if (page <= 0 || pageSize <= 0)
                return Result<List<ProductDto>>.Fail("Invalid page or pageSize. Must be greater than 0.", 400);

            if (!IsAdmin)
            {
                isActive = true;
                deletedOnly = false;
            }

            var cached = await _productCacheManger.GetProductListCacheAsync<ProductDto>(
                null, isActive, deletedOnly, pageSize, page, "BestSeller", IsAdmin);

            if (cached != null)
                return Result<List<ProductDto>>.Ok(cached, $"Found {cached.Count} best sellers", 200);

            try
            {
                var now = DateTime.UtcNow;

                var productBaseQuery = _unitOfWork.Product.GetAll();
                productBaseQuery = BasicFilter(productBaseQuery, isActive, deletedOnly, IsAdmin);

                var query = _unitOfWork.Repository<OrderItem>().GetAll()
                    .Where(i =>
                        i.Order.Status != OrderStatus.CancelledByAdmin &&
                        i.Order.Status != OrderStatus.CancelledByUser &&
                        i.Order.Status != OrderStatus.Returned &&
                        i.Order.Status != OrderStatus.Refunded &&
                        i.Order.Status != OrderStatus.PaymentExpired)
                    .GroupBy(i => i.ProductId)
                    .Select(g => new
                    {
                        ProductId = g.Key,
                        TotalQuantity = g.Sum(x => x.Quantity)
                    })
                    .Join(productBaseQuery,
                        g => g.ProductId,
                        p => p.Id,
                        (g, p) => new
                        {
                            Product = p,
                            g.TotalQuantity,
                            ValidDiscount =
                                p.Discount != null &&
                                p.Discount.IsActive &&
                                p.Discount.DeletedAt == null &&
                                p.Discount.EndDate > now
                        })
                    .OrderByDescending(x => x.TotalQuantity)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .Select(x => new ProductDto
                    {
                        Id = x.Product.Id,
                        Name = x.Product.Name,
                        Description = x.Product.Description,
                        AvailableQuantity = x.Product.Quantity,
                        Gender = x.Product.Gender,
                        SubCategoryId = x.Product.SubCategoryId,
                        Price = x.Product.Price,
                        CreatedAt = x.Product.CreatedAt,
                        ModifiedAt = x.Product.ModifiedAt,
                        DeletedAt = x.Product.DeletedAt,
                        fitType = x.Product.fitType,
                        IsActive = x.Product.IsActive,
                        TotalSold = IsAdmin? x.TotalQuantity:0,
                        FinalPrice =
                            x.ValidDiscount
                                ? x.Product.Price - ((x.Product.Discount!.DiscountPercent / 100m) * x.Product.Price)
                                : x.Product.Price,
                        EndAt =
                            IsAdmin
                                ? (x.Product.Discount != null ? x.Product.Discount.EndDate : null)
                                : (x.ValidDiscount ? x.Product.Discount!.EndDate : null),
                        DiscountName =
                            IsAdmin
                                ? (x.Product.Discount != null ? x.Product.Discount.Name : null)
                                : (x.ValidDiscount ? x.Product.Discount!.Name : null),
                        DiscountPrecentage =
                            IsAdmin
                                ? (x.Product.Discount != null ? x.Product.Discount.DiscountPercent : 0)
                                : (x.ValidDiscount ? x.Product.Discount!.DiscountPercent : 0),
                        images = x.Product.Images
                            .Where(i => i.DeletedAt == null)
                            .Select(i => new ImageDto
                            {
                                Id = i.Id,
                                Url = i.Url,
                                IsMain = i.IsMain
                            })
                    });

                var products = await query.ToListAsync();

                if (!products.Any())
                {
                    var fallbackQuery = productBaseQuery
                        .OrderBy(p => p.Id)
                        .Take(pageSize);

                    var fallbackProducts = await _productMapper
                        .maptoProductDtoexpression(fallbackQuery, IsAdmin)
                        .ToListAsync();

                    return Result<List<ProductDto>>.Ok(
                        fallbackProducts,
                        "No best sellers found. Showing fallback products instead.",
                        200);
                }

                var result = Result<List<ProductDto>>.Ok(
                    products,
                    $"Found {products.Count} best sellers",
                    200);

                _backgroundJobClient.Enqueue(() =>
                    _productCacheManger.SetProductListCacheAsync(
                        products, null, isActive, deletedOnly,
                        pageSize, page, "BestSeller", IsAdmin, null));

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in GetBestSellersAsync");
                _backgroundJobClient.Enqueue(() =>
                    _errorNotificationService.SendErrorNotificationAsync(ex.Message, ex.StackTrace));
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
                    searchCriteria.InStock = true;
                }

                var cached = await _productCacheManger.GetProductListCacheAsync<ProductDto>(
                    searchKey, isActive, deletedOnly, pageSize, page, "AdvancedSearch", IsAdmin);

                if (cached != null)
                    return Result<List<ProductDto>>.Ok(cached, $"Found {cached.Count} products matching your search", 200);

                var query = _unitOfWork.Product.GetAll();
                query = BasicFilter(query, isActive, deletedOnly, IsAdmin);

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

                var products = await _productMapper.maptoProductDtoexpression(query, IsAdmin)
                    .ToListAsync();

                if (products is null || !products.Any())
                    return Result<List<ProductDto>>.Fail("No products found matching the search criteria", 204);

                _backgroundJobClient.Enqueue(() => _productCacheManger.SetProductListCacheAsync<ProductDto>(products, searchKey, isActive, deletedOnly, pageSize, page, "AdvancedSearch", IsAdmin, null));

                return Result<List<ProductDto>>.Ok(products, $"Found {products.Count} products matching search criteria", 200);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in AdvancedSearchAsync");
                _backgroundJobClient.Enqueue(() => _errorNotificationService.SendErrorNotificationAsync(ex.Message, ex.StackTrace));
                return Result<List<ProductDto>>.Fail("Error performing advanced search", 500);
            }
        }

        public async Task<Result<List<BestSellingProductDto>>> GetBestSellerProductsWithCountAsync(bool? isDeleted, bool? isActive, int page = 1, int pagesize = 10, bool IsAdmin = false)
        {
            var cachedResult = await _productCacheManger.GetProductListCacheAsync<BestSellingProductDto>(null, isActive, isDeleted, pagesize, page, "BestSellerCount", IsAdmin);
            if (cachedResult is not null)
                return Result<List<BestSellingProductDto>>.Ok(cachedResult);

            var productsQuery = _unitOfWork.Product.GetAll().AsQueryable();
            productsQuery = BasicFilter(productsQuery, isActive, isDeleted, IsAdmin);

            var orderitemsquery = _unitOfWork.Repository<OrderItem>().GetAll().Where(oi =>
                oi.Order.Status != OrderStatus.CancelledByAdmin &&
                oi.Order.Status != OrderStatus.CancelledByUser &&
                oi.Order.Status != OrderStatus.Returned &&
                oi.Order.Status != OrderStatus.Refunded &&
                oi.Order.Status != OrderStatus.PaymentExpired
            );

            var bestSellers = await orderitemsquery
                .GroupBy(oi => oi.ProductId)
                .Select(g => new
                {
                    ProductId = g.Key,
                    TotalSold = g.Sum(x => x.Quantity)
                })
                .Join(productsQuery,
                    g => g.ProductId,
                    p => p.Id,
                    (g, p) => new BestSellingProductDto
                    {
                        ProductId = p.Id,
                        ProductName = p.Name,
                        TotalSoldQuantity = g.TotalSold,
                        Image = p.Images
                            .Where(i => i.IsMain && i.DeletedAt == null)
                            .Select(i => i.Url)
                            .FirstOrDefault()
                    })
                .OrderByDescending(x => x.TotalSoldQuantity)
                .Skip((page - 1) * pagesize)
                .Take(pagesize)
                .ToListAsync();

            _backgroundJobClient.Enqueue(() =>
                _productCacheManger.SetProductListCacheAsync(bestSellers, null, isActive, isDeleted, pagesize, page, "BestSellerCount", IsAdmin, null));

            return Result<List<BestSellingProductDto>>.Ok(bestSellers);
        }

        public async Task<Result<List<BestSellingProductDto>>> GetMostWishlistedProductsAsync(int page = 1, int pageSize = 10, bool? isActive = null, bool? deletedOnly = null, bool IsAdmin = false)
        {
            try
            {
                if (page <= 0 || pageSize <= 0)
                    return Result<List<BestSellingProductDto>>.Fail("Invalid page or pageSize. Must be greater than 0.", 400);

                if (!IsAdmin)
                {
                    isActive = true;
                    deletedOnly = false;
                }

                var cached = await _productCacheManger.GetProductListCacheAsync<BestSellingProductDto>(
                    null, isActive, deletedOnly, pageSize, page, "MostWishlisted", IsAdmin);

                if (cached != null)
                    return Result<List<BestSellingProductDto>>.Ok(cached, $"Found {cached.Count} most wishlisted products", 200);

                var wishCountQuery = _unitOfWork.Repository<WishlistItem>().GetAll()
                    .GroupBy(w => w.ProductId)
                    .Select(g => new
                    {
                        ProductId = g.Key,
                        Count = g.Count()
                    })
                    .OrderByDescending(g => g.Count);

                var productsQuery = _unitOfWork.Product.GetAll();
                productsQuery = BasicFilter(productsQuery, isActive, deletedOnly, IsAdmin);

                var joinedQuery = wishCountQuery
                    .Join(productsQuery,
                          w => w.ProductId,
                          p => p.Id,
                          (w, p) => new BestSellingProductDto
                          {
                              ProductId = p.Id,
                              ProductName = p.Name,
                              Image = p.Images.Where(i => i.IsMain && i.DeletedAt == null).Select(i => i.Url).FirstOrDefault() ?? "",
                              TotalSoldQuantity = w.Count
                          });

                var products = await joinedQuery
                    .OrderByDescending(x => x.TotalSoldQuantity)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToListAsync();

                if (!products.Any())
                {
                    var fallbackQuery = _unitOfWork.Product.GetAll();
                    fallbackQuery = BasicFilter(fallbackQuery, isActive, deletedOnly, IsAdmin);

                    var fallbackProducts = await fallbackQuery
                        .OrderBy(r => Guid.NewGuid())
                        .Take(pageSize)
                        .Select(p => new BestSellingProductDto
                        {
                            ProductId = p.Id,
                            ProductName = p.Name,
                            Image = p.Images.Where(i => i.IsMain && i.DeletedAt == null).Select(i => i.Url).FirstOrDefault() ?? "",
                            TotalSoldQuantity = 0
                        })
                        .ToListAsync();

                    return Result<List<BestSellingProductDto>>.Ok(fallbackProducts, "No wishlisted products found. Showing random products instead.", 200);
                }

                _backgroundJobClient.Enqueue(() => _productCacheManger.SetProductListCacheAsync(products, null, isActive, deletedOnly, pageSize, page, "MostWishlisted", IsAdmin, null));

                return Result<List<BestSellingProductDto>>.Ok(products, $"Found {products.Count} most wishlisted products", 200);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting most wishlisted products");
                _backgroundJobClient.Enqueue(() => _errorNotificationService.SendErrorNotificationAsync(ex.Message, ex.StackTrace));
                return Result<List<BestSellingProductDto>>.Fail("Error retrieving data", 500);
            }
        }

        public async Task<Result<ProductSalesDto>> GetProductSalesAsync(int productId)
        {
            var cached = await _productCacheManger.GetProductSalesCacheAsync(productId);
            if (cached != null)
                return Result<ProductSalesDto>.Ok(cached, $"Product sales report retrieved from cache", 200);

            try
            {
              
				var product = await _unitOfWork.Product.GetAll()
                    .Where(p => p.Id == productId)
                    .Select(p => new { p.Id, p.Name })
                    .FirstOrDefaultAsync();

                if (product is null)
                    return Result<ProductSalesDto>.Fail("Product not found.", 404);

                var invalidStatuses = new[]
                {
                    OrderStatus.CancelledByAdmin,
                    OrderStatus.CancelledByUser,
                    OrderStatus.Returned,
                    OrderStatus.Refunded,
                    OrderStatus.PaymentExpired
                };

                var variantSales = await _unitOfWork.Repository<OrderItem>().GetAll()
                    .Where(oi =>
                        oi.ProductVariant.ProductId == productId &&
                        !invalidStatuses.Contains(oi.Order.Status))
                    .GroupBy(oi => oi.ProductVariantId)
                    .Select(g => new
                    {
                        VariantId = g.Key,
                        TotalSold = g.Sum(x => x.Quantity)
                    })
                    .Join(
                        _unitOfWork.Repository<ProductVariant>().GetAll()
                            .Where(v => v.ProductId == productId),
                        g => g.VariantId,
                        v => v.Id,
                        (g, v) => new VariantSalesDto
                        {
                            VariantId = v.Id,
                            Color = v.Color,
                            Size = v.Size.HasValue ? v.Size.ToString() : null,
                            Waist = v.Waist,
                            Length = v.Length,
                            TotalSold = g.TotalSold,
                            RemainingQuantity=v.Quantity
						})
                    .OrderByDescending(v => v.TotalSold)
                    .ToListAsync();

                var totalSold = variantSales.Sum(v => v.TotalSold);

                var salesDto = new ProductSalesDto
                {
                    ProductId = product.Id,
                    ProductName = product.Name,
                    TotalSold = totalSold,
                    VariantSales = variantSales
                };

                _productCacheManger.SetProductSalesCacheAsync(productId, salesDto);

                return Result<ProductSalesDto>.Ok(salesDto, $"Product '{product.Name}' has sold {totalSold} units in total.", 200);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in GetProductSalesAsync for productId={ProductId}", productId);
                _backgroundJobClient.Enqueue(() => _errorNotificationService.SendErrorNotificationAsync(ex.Message, ex.StackTrace));
                return Result<ProductSalesDto>.Fail("Error retrieving product sales data.", 500);
            }
        }
    }
}
