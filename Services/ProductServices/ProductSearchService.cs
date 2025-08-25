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
using E_Commerce.Services.Cache;
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
		public  Task<Result<List<BestSellingProductDto>>> GetBestSellerProductsWithCountAsync(bool? isDeleted, bool? isActive, int page = 1, int pagesize = 10);
		Task<Result<List<ProductDto>>> GetNewArrivalsAsync(int page, int pageSize, bool? isActive = null, bool? deletedOnly = null);
		Task<Result<List<ProductDto>>> GetBestSellersAsync(int page, int pageSize, bool? isActive = null, bool? deletedOnly = null);
		Task<Result<List<ProductDto>>> AdvancedSearchAsync(AdvancedSearchDto searchCriteria, int page, int pageSize, bool? isActive = null, bool? deletedOnly = null);
	}

	public class ProductSearchService : IProductSearchService
	{
		private readonly IBackgroundJobClient _backgroundJobClient;
		private readonly IUnitOfWork _unitOfWork;
		private readonly ILogger<ProductSearchService> _logger;
		private readonly IErrorNotificationService _errorNotificationService;
		private readonly ICacheManager _cacheManager;
		public const string CACHE_TAG_PRODUCT_SEARCH = "product_search";
		public const string CACHE_TAG_SUBCATEGORY = "subcategory";
		public static readonly string[] PRODUCT_CACHE_TAGS = new[] { CACHE_TAG_PRODUCT_SEARCH, CACHE_TAG_SUBCATEGORY };

		public ProductSearchService(
			IBackgroundJobClient backgroundJobClient,
			IUnitOfWork unitOfWork,
			ILogger<ProductSearchService> logger,
			IErrorNotificationService errorNotificationService,
			ICacheManager cacheManager)
		{
			_backgroundJobClient = backgroundJobClient;
			_unitOfWork = unitOfWork;
			_logger = logger;
			_errorNotificationService = errorNotificationService;
			_cacheManager = cacheManager;
		}



		private IQueryable<E_Commerce.Models.Product> BasicFilter(IQueryable<E_Commerce.Models.Product> query, bool? isActive, bool? DeletedOnly)
		{
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


		private Expression<Func<Product, ProductDto>> MapToProductdto => p => new ProductDto
		{
			Id = p.Id,
			Name = p.Name,
			Description = p.Description,
			AvailableQuantity = p.Quantity,
			Gender = p.Gender,
			SubCategoryId = p.SubCategoryId,
			Price = p.Price,
			CreatedAt = p.CreatedAt,
			ModifiedAt = p.ModifiedAt,
			DeletedAt = p.DeletedAt,
			fitType = p.fitType,
			IsActive = p.IsActive,


			FinalPrice = (p.Discount != null &&
						  p.Discount.IsActive &&
						  p.Discount.DeletedAt == null &&
						  p.Discount.EndDate > DateTime.UtcNow)
						  ? p.Price - ((p.Discount.DiscountPercent / 100m) * p.Price)
						  : p.Price,


			EndAt = (p.Discount != null &&
					 p.Discount.IsActive &&
					 p.Discount.DeletedAt == null &&
					 p.Discount.EndDate > DateTime.UtcNow)
					 ? p.Discount.EndDate
					 : null,


			DiscountName = (p.Discount != null &&
							p.Discount.IsActive &&
							p.Discount.DeletedAt == null &&
							p.Discount.EndDate > DateTime.UtcNow)
							? p.Discount.Name
							: null,

			DiscountPrecentage = (p.Discount != null &&
								  p.Discount.IsActive &&
								  p.Discount.DeletedAt == null &&
								  p.Discount.EndDate > DateTime.UtcNow)
								  ? p.Discount.DiscountPercent
								  : 0,


			images = p.Images

				.Select(i => new ImageDto
				{
					Id = i.Id,
					Url = i.Url
				})
		};






		public async Task<Result<List<ProductDto>>> GetNewArrivalsAsync(int page, int pageSize, bool? isActive = null, bool? deletedOnly = null)
		{
			if (page <= 0 || pageSize <= 0)
				return Result<List<ProductDto>>.Fail("Invalid page or pageSize. Must be greater than 0.", 400);
			string cacheKey = $"newarrivals_{page}_{pageSize}_{isActive}_{deletedOnly}";
			var cached = await _cacheManager.GetAsync<Result<List<ProductDto>>>(cacheKey);
			if (cached != null)
				return cached;
			try
			{
				var thirtyDaysAgo = DateTime.UtcNow.AddDays(-90);
				var query = _unitOfWork.Product.GetAll().Where(p => p.CreatedAt >= thirtyDaysAgo);


				query = BasicFilter(query, isActive, deletedOnly);
				// Only order by descending CreatedAt for new arrivals
				var products = await query.Select(MapToProductdto)
					.OrderByDescending(p => p.CreatedAt)
					.Skip((page - 1) * pageSize)
					.Take(pageSize)
					.ToListAsync();


				if (!products.Any())
					return Result<List<ProductDto>>.Fail("No new arrivals found", 404);


				BackgroundJob.Enqueue(() => _cacheManager.SetAsync(cacheKey, products, TimeSpan.FromMinutes(2), new[] { CACHE_TAG_PRODUCT_SEARCH }));
				return Result<List<ProductDto>>.Ok(products, $"Found {products.Count} new arrivals", 200); ;
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error in GetNewArrivalsAsync");
				_backgroundJobClient.Enqueue(() => _errorNotificationService.SendErrorNotificationAsync(ex.Message, ex.StackTrace));
				return Result<List<ProductDto>>.Fail("Error retrieving new arrivals", 500);
			}
		}

		public async Task<Result<List<ProductDto>>> GetBestSellersAsync(int page, int pageSize, bool? isActive = null, bool? deletedOnly = null)
		{
			if (page <= 0 || pageSize <= 0)
				return Result<List<ProductDto>>.Fail("Invalid page or pageSize. Must be greater than 0.", 400);

			string cacheKey = $"bestsellers_{page}_{pageSize}_{isActive}_{deletedOnly}";
			var cached = await _cacheManager.GetAsync<Result<List<ProductDto>>>(cacheKey);
			if (cached != null)
				return cached;

			try
			{
				var bestSellerQuery = _unitOfWork.Repository<OrderItem>().GetAll()
					.Where(i => i.Order.Status != OrderStatus.CancelledByAdmin|| i.Order.Status != OrderStatus.CancelledByUser)
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

				productQuery = BasicFilter(productQuery, isActive, deletedOnly);

				var products = await productQuery
					.Select(MapToProductdto)
					.Skip((page - 1) * pageSize)
					.Take(pageSize)
					.ToListAsync();

				if (!products.Any())
				{
					var fallbackProducts = await _unitOfWork.Product.GetAll()
						.Where(p => isActive == null || p.IsActive == isActive)
						.OrderBy(r => Guid.NewGuid())
						.Take(pageSize)
						.Select(MapToProductdto)
						.ToListAsync();

					return Result<List<ProductDto>>.Ok(fallbackProducts, "No best sellers found. Showing random products instead.", 200);
				}

				var result = Result<List<ProductDto>>.Ok(products, $"Found {products.Count} best sellers", 200);

				BackgroundJob.Enqueue(() => _cacheManager.SetAsync(cacheKey, result, TimeSpan.FromMinutes(2), new[] { CACHE_TAG_PRODUCT_SEARCH }));

				return result;
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error in GetBestSellersAsync");
				_backgroundJobClient.Enqueue(() => _errorNotificationService.SendErrorNotificationAsync(ex.Message, ex.StackTrace));
				return Result<List<ProductDto>>.Fail("Error retrieving best sellers", 500);
			}
		}

		public async Task<Result<List<ProductDto>>> AdvancedSearchAsync(AdvancedSearchDto searchCriteria, int page, int pageSize, bool? isActive = null, bool? deletedOnly = null)
		{
			if (page <= 0 || pageSize <= 0)
				return Result<List<ProductDto>>.Fail("Invalid page or pageSize. Must be greater than 0.", 400);
			try
			{
				string cacheKey = $"advsearch_{searchCriteria?.SearchTerm}_{searchCriteria?.Subcategoryid}_{searchCriteria?.Gender}_{searchCriteria?.FitType}_{searchCriteria?.MinPrice}_{searchCriteria?.MaxPrice}_{searchCriteria?.InStock}_{searchCriteria?.OnSale}_{searchCriteria?.SortBy}_{searchCriteria?.SortDescending}_{page}_{pageSize}_{isActive}_{deletedOnly}";


				var cached = await _cacheManager.GetAsync<Result<List<ProductDto>>>(cacheKey);
				if (cached != null)
					return cached;

				var query = _unitOfWork.Product.GetAll();
				query = BasicFilter(query, isActive, deletedOnly);

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

				// Projection and pagination
				var products = await query
					.Skip((page - 1) * pageSize)
					.Take(pageSize)
					.Select(MapToProductdto)
					.ToListAsync();



				if (!products.Any())
					return Result<List<ProductDto>>.Fail("No products found matching the search criteria", 404);

				BackgroundJob.Enqueue(() => _cacheManager.SetAsync(cacheKey, products, TimeSpan.FromMinutes(2), new[] { CACHE_TAG_PRODUCT_SEARCH }));

				return Result<List<ProductDto>>.Ok(products, $"Found {products.Count} products matching search criteria", 200);
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error in AdvancedSearchAsync");
				_backgroundJobClient.Enqueue(() => _errorNotificationService.SendErrorNotificationAsync(ex.Message, ex.StackTrace));
				return Result<List<ProductDto>>.Fail("Error performing advanced search", 500);
			}
		}


		public async Task<Result<List<BestSellingProductDto>>> GetBestSellerProductsWithCountAsync(bool? isDeleted, bool? isActive,int page=1,int pagesize=10)
		{
			var cacheKey = $"BestSellerProducts:isDeleted={isDeleted}_isActive={isActive}_page={page}_pagesize_{pagesize}";

			var cachedResult = await _cacheManager.GetAsync<List<BestSellingProductDto>>(cacheKey);
			if (cachedResult is not null)
				return Result<List<BestSellingProductDto>>.Ok(cachedResult);

			var productsQuery = _unitOfWork.Product.GetAll()
				.Include(p => p.ProductVariants)
					.ThenInclude(v => v.OrderItems)
				.Include(p => p.Images)
				.AsQueryable();

			productsQuery = BasicFilter(productsQuery, isActive, isDeleted);

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

			BackgroundJob.Enqueue(() =>
				_cacheManager.SetAsync(cacheKey, bestSellers, TimeSpan.FromHours(1), new[] { CACHE_TAG_PRODUCT_SEARCH }));

			return Result<List<BestSellingProductDto>>.Ok(bestSellers);
		}



	}
	public class BestSellingProductDto
	{
		public int ProductId { get; set; }
		public string ProductName { get; set; } = string.Empty;
		public string? Image { get; set; }
		public int TotalSoldQuantity { get; set; }
	}

	public class AdvancedSearchDto
	{
		public string? SearchTerm { get; set; }
		public int? Subcategoryid { get; set; }
		public Gender? Gender { get; set; }
		public FitType? FitType { get; set; }
		public decimal? MinPrice { get; set; }
		public decimal? MaxPrice { get; set; }
		public bool? InStock { get; set; }
		public bool? OnSale { get; set; }
		public string? SortBy { get; set; }
		public bool SortDescending { get; set; } = false;
		public string? Color { get; set; } // Filter by variant color
		public decimal? MinSize { get; set; } // Minimum variant size
		public decimal? MaxSize { get; set; } // Maximum variant size
	}
}
