using E_Commerce.DtoModels.DiscoutDtos;
using E_Commerce.DtoModels.ProductDtos;
using E_Commerce.DtoModels.Responses;
using E_Commerce.Enums;
using E_Commerce.ErrorHnadling;
using E_Commerce.Interfaces;
using E_Commerce.Models;
using E_Commerce.Services.AdminOpreationServices;
using E_Commerce.Services.Cache;
using E_Commerce.Services.EmailServices;
using E_Commerce.Services.ProductServices;
using E_Commerce.UOW;
using Hangfire;
using Microsoft.EntityFrameworkCore;
using Order = E_Commerce.Models.Order;

namespace E_Commerce.Services.Discount
{
	public class DiscountService : IDiscountService
	{
		private readonly IBackgroundJobClient _backgroundJobClient;
		private readonly IProductDiscountService _productDiscountService;
		private readonly ICartServices _cartServices;
		private readonly IOrderServices _orderServices;
		private readonly IUnitOfWork _unitOfWork;
		private readonly ILogger<DiscountService> _logger;
		private readonly IAdminOpreationServices _adminOpreationServices;
		private readonly IErrorNotificationService _errorNotificationService;
		private readonly ICacheManager _cacheManager;
		private const string CACHE_TAG_PRODUCT_SEARCH = "product_search";

		public const string CACHE_TAG_CATEGORY_WITH_DATA = "categorywithdata";
		private static readonly string[] PRODUCT_CACHE_TAGS = new[] { CACHE_TAG_PRODUCT_SEARCH, CACHE_TAG_CATEGORY_WITH_DATA, PRODUCT_WITH_VARIANT_TAG };
		private const string PRODUCT_WITH_VARIANT_TAG = "productwithvariantdata";

		public DiscountService(
			IOrderServices orderServices,
			ICartServices cartServices,
			ICacheManager cacheManager,
			IProductDiscountService productDiscountService,
			IBackgroundJobClient backgroundJobClient,	
			IUnitOfWork unitOfWork,
			ILogger<DiscountService> logger,
			IAdminOpreationServices adminOpreationServices,
			IErrorNotificationService errorNotificationService)
		{
			_cartServices = cartServices;
			_orderServices=orderServices;
			_cacheManager = cacheManager;
			_productDiscountService = productDiscountService;
			_backgroundJobClient = backgroundJobClient;
			_unitOfWork = unitOfWork;
			_logger = logger;
			_adminOpreationServices = adminOpreationServices;
			_errorNotificationService = errorNotificationService;
		}
		private void SchedulingToCheckDiscounts(int id,DateTime start,DateTime end)
		{
			
			_backgroundJobClient.Schedule(() => CheckOnDiscount(id), start);
			_backgroundJobClient.Schedule(() => CheckOnDiscount(id), end);

		}
		private void RemoveProductCaches()
		{

			BackgroundJob.Enqueue(() => _cacheManager.RemoveByTagsAsync(PRODUCT_CACHE_TAGS));
		}
		public async Task CheckOnDiscount(int id)
		{
			var discount = await _unitOfWork.Repository<E_Commerce.Models.Discount>().GetByIdAsync(id);
			if( discount == null)
			{
				_logger.LogWarning($"Discount with ID {id} not found for check.");
				return;
			}
			bool shouldDeactivate = discount.IsActive &&
						((discount.EndDate >DateTime.UtcNow && discount.EndDate < DateTime.UtcNow) || discount.DeletedAt != null);

			bool shouldActivate = !discount.IsActive &&
								  discount.EndDate >= DateTime.UtcNow &&
								  discount.StartDate <= DateTime.UtcNow;

			if (shouldDeactivate)
			{
				_logger.LogInformation($"Discount with ID {id} has expired. Deactivating it.");
				discount.IsActive = false;
				var productsids = await  _unitOfWork.Product.GetAll().Where(p => p.DiscountId == id && p.DeletedAt == null).Select(p => p.Id).ToListAsync();
				_backgroundJobClient.Enqueue(() => _orderServices.UpdateOrderItemsForProductsAfterRemoveDiscountAsync(productsids));
				_backgroundJobClient.Enqueue(() => _cartServices.UpdateCartItemsForProductsAfterRemoveDiscountAsync(productsids));

			}
			else if (shouldActivate)
			{
				_logger.LogInformation($"Discount with ID {id} is now valid. Activating it.");
				discount.IsActive = true;
				var productsids = await _unitOfWork.Product.GetAll().Where(p => p.DiscountId == id && p.DeletedAt == null).Select(p => p.Id).ToListAsync();
				_backgroundJobClient.Enqueue(() => _orderServices.UpdateOrderItemsForProductsAfterAddDiscountAsync(productsids,discount.DiscountPercent));
				_backgroundJobClient.Enqueue(() => _cartServices.UpdateCartItemsForProductsAfterAddDiscountAsync(productsids,discount.DiscountPercent));
			}

			if (shouldDeactivate || shouldActivate)
			{
			
				RemoveProductCaches();
				await _unitOfWork.CommitAsync();
			}


		}
		public async Task<Result<List<DiscountDto>>> GetAllAsync()
		{
			try
			{
				var discounts = await _unitOfWork.Repository<Models.Discount>().GetAll().AsNoTracking()
					.Where(d => d.DeletedAt == null)
					.Select(d => new DiscountDto
					{
						Id = d.Id,
						Name = d.Name,
						Description = d.Description,
						DiscountPercent = d.DiscountPercent,
						StartDate = d.StartDate,
						EndDate = d.EndDate,
						DeletedAt= d.DeletedAt,
						IsActive = d.IsActive,
						CreatedAt = d.CreatedAt,
						ModifiedAt = d.ModifiedAt
					})
					.ToListAsync();

				if (!discounts.Any())
					return Result<List<DiscountDto>>.Fail("No discounts found", 404);

				return Result<List<DiscountDto>>.Ok(discounts, "All discounts retrieved successfully", 200);
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error in GetAllAsync");
				 	_backgroundJobClient.Enqueue(()=> _errorNotificationService.SendErrorNotificationAsync(ex.Message, ex.StackTrace));
				return Result<List<DiscountDto>>.Fail("Error retrieving discounts", 500);
			}
		}

		public async Task<Result<DiscountDto>> GetDiscountByIdAsync(int id, bool? isActive = null, bool? isDeleted = false)
		{
			try
			{
				var query = _unitOfWork.Repository<Models.Discount>().GetAll().AsNoTracking().Where(d => d.Id == id);

				if (isDeleted.HasValue){
					if(isDeleted.Value)
						query = query.Where(d => d.DeletedAt != null);
					else
						query = query.Where(d => d.DeletedAt == null);
				}
				if (isActive.HasValue)
					query = query.Where(d => d.IsActive == isActive.Value);

				var discount = await query
					.Select(d => new DiscountDto
					{
						Id = d.Id,
						Name = d.Name,
						Description = d.Description,
						DiscountPercent = d.DiscountPercent,
						StartDate = d.StartDate,
						EndDate = d.EndDate,
						IsActive = d.IsActive,
						CreatedAt = d.CreatedAt,
						ModifiedAt = d.ModifiedAt,
						DeletedAt = d.DeletedAt
					})
					.FirstOrDefaultAsync();

				if (discount == null)
					return Result<DiscountDto>.Fail("Discount not found", 404);

				return Result<DiscountDto>.Ok(discount, "Discount retrieved successfully", 200);
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, $"Error in GetDiscountByIdAsync for id: {id}");
				 	_backgroundJobClient.Enqueue(()=> _errorNotificationService.SendErrorNotificationAsync(ex.Message, ex.StackTrace));
				return Result<DiscountDto>.Fail("Error retrieving discount", 500);
			}
		}
		public async  Task<Result<List< DiscountDto>>> GetDiscountByNameAsync(string name, bool? isActive = null, bool? isDeleted = null)
		{
			try
			{
				var query = _unitOfWork.Repository<Models.Discount>().GetAll().AsNoTracking()
					.Where(d => d.Name.Contains( name)||d.Description.Contains(name));
				if (isDeleted.HasValue)
				{
					if (isDeleted.Value)
						query = query.Where(d => d.DeletedAt != null);
					else
						query = query.Where(d => d.DeletedAt == null);
				}
				if (isActive.HasValue)
					query = query.Where(d => d.IsActive == isActive.Value);
				var discount = await query
					.Select(d => new DiscountDto
					{
						Id = d.Id,
						Name = d.Name,
						Description = d.Description,
						DiscountPercent = d.DiscountPercent,
						StartDate = d.StartDate,
						EndDate = d.EndDate,
						IsActive = d.IsActive,
						CreatedAt = d.CreatedAt,
						ModifiedAt = d.ModifiedAt,
						DeletedAt = d.DeletedAt
					}).ToListAsync();

				if (discount == null)
					return Result<List<DiscountDto>>.Fail("Discount not found", 404);
				return Result<List<DiscountDto>>.Ok(discount, "Discount retrieved successfully", 200);
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, $"Error in GetDiscountByNameAsync for name: {name}");
					_backgroundJobClient.Enqueue(()=> _errorNotificationService.SendErrorNotificationAsync(ex.Message, ex.StackTrace));
				return Result<List<DiscountDto>>.Fail("Error retrieving discount", 500);
			}
		}

		public async Task<Result<DiscountDto>> CreateDiscountAsync(CreateDiscountDto dto, string userId)
		{
			_logger.LogInformation($"Creating new discount: {dto.Name}");
			try
			{
				
				if (dto.StartDate >= dto.EndDate)
					return Result<DiscountDto>.Fail("Start date must be before end date", 400);

				if(dto.EndDate < DateTime.UtcNow)
					return Result<DiscountDto>.Fail("End date cannot be in the past", 400);

				var existingDiscount = await _unitOfWork.Repository<Models.Discount>().GetAll().AsNoTracking()
					.FirstOrDefaultAsync(d => d.Name == dto.Name && d.DeletedAt == null);

				if (existingDiscount != null)
					return Result<DiscountDto>.Fail($"Discount with name '{dto.Name}' already exists", 409);

				using var transaction = await _unitOfWork.BeginTransactionAsync();

				var discount = new Models.Discount
				{
					Name = dto.Name,
					Description = dto.Description,
					DiscountPercent = dto.DiscountPercent,
					StartDate = dto.StartDate,
					EndDate = dto.EndDate,
				};

				var result = await _unitOfWork.Repository<Models.Discount>().CreateAsync(discount);
				if (result == null)
				{
					await transaction.RollbackAsync();
					return Result<DiscountDto>.Fail("Failed to create discount", 400);
				}

				// Log admin operation
				await _adminOpreationServices.AddAdminOpreationAsync(
					$"Create Discount {discount.Id}",
					Opreations.AddOpreation,
					userId,
					discount.Id
				);

				await _unitOfWork.CommitAsync();
				await transaction.CommitAsync();

				SchedulingToCheckDiscounts(discount.Id, discount.StartDate, discount.EndDate);

				var discountDto = new DiscountDto
				{
					Id = discount.Id,
					Name = discount.Name,
					Description = discount.Description,
					DiscountPercent = discount.DiscountPercent,
					StartDate = discount.StartDate,
					EndDate = discount.EndDate,
					IsActive = discount.IsActive,
					CreatedAt = discount.CreatedAt
				};

				return Result<DiscountDto>.Ok(discountDto, "Discount created successfully", 201);
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, $"Error in CreateDiscountAsync for discount {dto.Name}");
				 	_backgroundJobClient.Enqueue(()=> _errorNotificationService.SendErrorNotificationAsync(ex.Message, ex.StackTrace));
				return Result<DiscountDto>.Fail("Error creating discount", 500);
			}
		}

		public async Task<Result<DiscountDto>> UpdateDiscountAsync(int id, UpdateDiscountDto dto, string userId)
		{
			_logger.LogInformation($"Updating discount: {id}");
			try
			{
				var discount = await _unitOfWork.Repository<Models.Discount>().GetByIdAsync(id);
				if (discount == null)
					return Result<DiscountDto>.Fail("Discount not found", 404);

				using var transaction = await _unitOfWork.BeginTransactionAsync();

				// Track changes
				var changes = new List<string>();

				if (!string.IsNullOrEmpty(dto.Name) && dto.Name != discount.Name)
				{
					changes.Add($"Name changed from '{discount.Name}' to '{dto.Name}'");
					discount.Name = dto.Name;
				}
				if (!string.IsNullOrEmpty(dto.Description) && dto.Description != discount.Description)
				{
					changes.Add($"Description changed from '{discount.Description}' to '{dto.Description}'");
					discount.Description = dto.Description;
				}
				if (dto.DiscountPercent.HasValue && dto.DiscountPercent.Value != discount.DiscountPercent)
				{
					changes.Add($"DiscountPercent changed from {discount.DiscountPercent}% to {dto.DiscountPercent.Value}%");
					discount.DiscountPercent = dto.DiscountPercent.Value;
				}
				if (dto.StartDate.HasValue && dto.StartDate.Value != discount.StartDate)
				{
					changes.Add($"StartDate changed from {discount.StartDate} to {dto.StartDate.Value}");
					discount.StartDate = dto.StartDate.Value;
				}
				if (dto.EndDate.HasValue && dto.EndDate.Value != discount.EndDate&& discount.EndDate < DateTime.UtcNow)
				{
					changes.Add($"EndDate changed from {discount.EndDate} to {dto.EndDate.Value}");
					discount.EndDate = dto.EndDate.Value;
				}
				
				if (!changes.Any())
				{
					return Result<DiscountDto>.Fail("No changes were provided to update.", 400);
				}

				// Validate dates
				if (discount.StartDate >= discount.EndDate)
					return Result<DiscountDto>.Fail("Start date must be before end date", 400);

				var result = _unitOfWork.Repository<Models.Discount>().Update(discount);
				if (!result)
				{
					await transaction.RollbackAsync();
					return Result<DiscountDto>.Fail("Failed to update discount", 400);
				}

				// Log admin operation with detailed changes
				string changeSummary = string.Join(" | ", changes);
				await _adminOpreationServices.AddAdminOpreationAsync(
					$"Updated Discount {id}: {changeSummary}",
					Opreations.UpdateOpreation,
					userId,
					id
				);

				await _unitOfWork.CommitAsync();
				await transaction.CommitAsync();
				SchedulingToCheckDiscounts(discount.Id, discount.StartDate, discount.EndDate);

				var discountDto = new DiscountDto
				{
					Id = discount.Id,
					Name = discount.Name,
					Description = discount.Description,
					DiscountPercent = discount.DiscountPercent,
					StartDate = discount.StartDate,
					EndDate = discount.EndDate,
					IsActive = discount.IsActive,
					CreatedAt = discount.CreatedAt,
					DeletedAt = discount.DeletedAt,
					ModifiedAt = discount.ModifiedAt
				};

				return Result<DiscountDto>.Ok(discountDto, "Discount updated successfully", 200);
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, $"Error in UpdateDiscountAsync for id: {id}");
				_backgroundJobClient.Enqueue(() => _errorNotificationService.SendErrorNotificationAsync(ex.Message, ex.StackTrace));
				return Result<DiscountDto>.Fail("Error updating discount", 500);
			}
		}


		public async Task<Result<bool>> DeleteDiscountAsync(int id, string userId)
		{
			_logger.LogInformation($"Deleting discount: {id}");
			try
			{
				var discount = await _unitOfWork.Repository<Models.Discount>().GetByIdAsync(id);
				if (discount == null||discount.DeletedAt!=null)
					return Result<bool>.Fail("Discount not found", 404);

				using var transaction = await _unitOfWork.BeginTransactionAsync();

				var result = await _unitOfWork.Repository<Models.Discount>().SoftDeleteAsync(id);
				discount.IsActive= false;
				if (!result)
				{
					await transaction.RollbackAsync();
					return Result<bool>.Fail("Failed to delete discount", 400);
				}

				// Log admin operation
				await _adminOpreationServices.AddAdminOpreationAsync(
					$"Delete Discount {id}",
					Opreations.DeleteOpreation,
					userId,
					id
				);

				await _unitOfWork.CommitAsync();
				await transaction.CommitAsync();
				var productsids = await _unitOfWork.Product.GetAll().Where(p => p.DiscountId == id && p.DeletedAt == null).Select(p => p.Id).ToListAsync();
				_backgroundJobClient.Enqueue(() => _orderServices.UpdateOrderItemsForProductsAfterRemoveDiscountAsync(productsids));
				_backgroundJobClient.Enqueue(() => _cartServices.UpdateCartItemsForProductsAfterRemoveDiscountAsync(productsids));
				RemoveProductCaches();
				return Result<bool>.Ok(true, "Discount deleted", 200);
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, $"Error in DeleteDiscountAsync for id: {id}");
				 	_backgroundJobClient.Enqueue(()=> _errorNotificationService.SendErrorNotificationAsync(ex.Message, ex.StackTrace));
				return Result<bool>.Fail("Error deleting discount", 500);
			}
		}

		public async Task<Result<DiscountDto>> RestoreDiscountAsync(int id, string userId)
		{
			_logger.LogInformation($"Restoring discount: {id}");
			try
			{
				var discount = await _unitOfWork.Repository<Models.Discount>().GetByIdAsync(id);
				if (discount == null)
					return Result<DiscountDto>.Fail("Discount not found", 404);

				using var transaction = await _unitOfWork.BeginTransactionAsync();

				discount.DeletedAt = null;
				var result = _unitOfWork.Repository<Models.Discount>().Update(discount);
				if (!result)
				{
					await transaction.RollbackAsync();
					return Result<DiscountDto>.Fail("Failed to restore discount", 400);
				}

				// Log admin operation
				await _adminOpreationServices.AddAdminOpreationAsync(
					$"Restore Discount {id}",
					Opreations.UpdateOpreation,
					userId,
					id
				);

				await _unitOfWork.CommitAsync();
				await transaction.CommitAsync();
				RemoveProductCaches();

				var discountDto = new DiscountDto
				{
					Id = discount.Id,
					Name = discount.Name,
					Description = discount.Description,
					DiscountPercent = discount.DiscountPercent,
					StartDate = discount.StartDate,
					EndDate = discount.EndDate,
					IsActive = discount.IsActive,
					CreatedAt = discount.CreatedAt,
					ModifiedAt = discount.ModifiedAt
				};

				return Result<DiscountDto>.Ok(discountDto, "Discount restored successfully", 200);
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, $"Error in RestoreDiscountAsync for id: {id}");
				 	_backgroundJobClient.Enqueue(()=> _errorNotificationService.SendErrorNotificationAsync(ex.Message, ex.StackTrace));
				return Result<DiscountDto>.Fail("Error restoring discount", 500);
			}
		}

		public async Task<Result<List<DiscountDto>>> FilterAsync(string? search, bool? isActive, bool? IsDeleted, int page, int pageSize, string role)
		{
			try
			{
				var query = _unitOfWork.Repository<Models.Discount>().GetAll().AsNoTracking();

				if(IsDeleted.HasValue)
				{
					if (IsDeleted.Value)
						query = query.Where(d => d.DeletedAt != null);
					else
						query = query.Where(d => d.DeletedAt == null);
				}

				if (!string.IsNullOrWhiteSpace(search))
					query = query.Where(d => d.Name.Contains(search) || d.Description.Contains(search));

				if (isActive.HasValue)
					query = query.Where(d => d.IsActive == isActive.Value);

				var totalCount = await query.CountAsync();

				var discounts = await query
					.OrderBy(d => d.CreatedAt)
					.Skip((page - 1) * pageSize)
					.Take(pageSize)
					.Select(d => new DiscountDto
					{
						Id = d.Id,
						Name = d.Name,
						Description = d.Description,
						DiscountPercent = d.DiscountPercent,
						StartDate = d.StartDate,
						EndDate = d.EndDate,
						IsActive = d.IsActive,
						CreatedAt = d.CreatedAt,
						ModifiedAt = d.ModifiedAt
					})
					.ToListAsync();

				if (!discounts.Any())
					return Result<List<DiscountDto>>.Fail("No discounts found matching criteria", 404);

				return Result<List<DiscountDto>>.Ok(discounts, $"Found {discounts.Count} discounts out of {totalCount}", 200);
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error in FilterAsync");
				 	_backgroundJobClient.Enqueue(()=> _errorNotificationService.SendErrorNotificationAsync(ex.Message, ex.StackTrace));
				return Result<List<DiscountDto>>.Fail("Error filtering discounts", 500);
			}
		}

		public async Task<Result<List<DiscountDto>>> GetActiveDiscountsAsync()
		{
			try
			{
				var now = DateTime.UtcNow;
				var discounts = await _unitOfWork.Repository<Models.Discount>().GetAll().AsNoTracking()
					.Where(d => d.DeletedAt == null && 
						d.IsActive && 
						d.StartDate <= now && 
						d.EndDate >= now)
					.Select(d => new DiscountDto
					{
						Id = d.Id,
						Name = d.Name,
						Description = d.Description,
						DiscountPercent = d.DiscountPercent,
						StartDate = d.StartDate,
						EndDate = d.EndDate,
						IsActive = d.IsActive,
						CreatedAt = d.CreatedAt
					})
					.ToListAsync();

				if (!discounts.Any())
					return Result<List<DiscountDto>>.Fail("No active discounts found", 404);

				return Result<List<DiscountDto>>.Ok(discounts, "Active discounts retrieved successfully", 200);
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error in GetActiveDiscountsAsync");
				 	_backgroundJobClient.Enqueue(()=> _errorNotificationService.SendErrorNotificationAsync(ex.Message, ex.StackTrace));
				return Result<List<DiscountDto>>.Fail("Error retrieving active discounts", 500);
			}
		}

		public async Task<Result<List<DiscountDto>>> GetExpiredDiscountsAsync()
		{
			try
			{
				var now = DateTime.UtcNow;

				var expiredDiscounts = await _unitOfWork.Repository<Models.Discount>().GetAll().AsNoTracking()
					.Where(d => d.DeletedAt == null && d.EndDate < now)
					.OrderByDescending(d => d.EndDate)
					.Select(d => new DiscountDto
					{
						Id = d.Id,
						Name = d.Name,
						Description = d.Description,
						DiscountPercent = d.DiscountPercent,
						StartDate = d.StartDate,
						EndDate = d.EndDate,
						IsActive = d.IsActive,
						CreatedAt = d.CreatedAt
					})
					.ToListAsync();

				if (expiredDiscounts.Count == 0)
					return Result<List<DiscountDto>>.Ok(new List<DiscountDto>(), "No expired discounts found", 200);

				return Result<List<DiscountDto>>.Ok(expiredDiscounts, "Expired discounts retrieved successfully", 200);
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error in GetExpiredDiscountsAsync");
				_backgroundJobClient.Enqueue(() => _errorNotificationService.SendErrorNotificationAsync(ex.Message, ex.StackTrace));
				return Result<List<DiscountDto>>.Fail("Error retrieving expired discounts", 500);
			}
		}


		public async Task<Result<List<DiscountDto>>> GetUpcomingDiscountsAsync()
		{
			try
			{
				var now = DateTime.UtcNow;

				var upcomingDiscounts = await _unitOfWork.Repository<Models.Discount>().GetAll().AsNoTracking()
					.Where(d => d.DeletedAt == null && d.StartDate > now)
					.OrderBy(d => d.StartDate)
					.Select(d => new DiscountDto
					{
						Id = d.Id,
						Name = d.Name,
						Description = d.Description,
						DiscountPercent = d.DiscountPercent,
						StartDate = d.StartDate,
						EndDate = d.EndDate,
						IsActive = d.IsActive,
						CreatedAt = d.CreatedAt
					})
					.ToListAsync();

				if (upcomingDiscounts.Count == 0)
					return Result<List<DiscountDto>>.Ok(new List<DiscountDto>(), "No upcoming discounts found", 200); // 200 + empty list

				return Result<List<DiscountDto>>.Ok(upcomingDiscounts, "Upcoming discounts retrieved successfully", 200);
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error in GetUpcomingDiscountsAsync");
				_backgroundJobClient.Enqueue(() => _errorNotificationService.SendErrorNotificationAsync(ex.Message, ex.StackTrace));
				return Result<List<DiscountDto>>.Fail("Error retrieving upcoming discounts", 500);
			}
		}


		public async Task<Result<List<DiscountDto>>> GetDiscountsByCategoryAsync(int categoryId)
		{
			try
			{
				var discounts = await _unitOfWork.Repository<Models.Discount>().GetAll().AsNoTracking()
					.Where(d => d.DeletedAt == null && d.CategoryId == categoryId)
					.Select(d => new DiscountDto
					{
						Id = d.Id,
						Name = d.Name,
						Description = d.Description,
						DiscountPercent = d.DiscountPercent,
						StartDate = d.StartDate,
						EndDate = d.EndDate,
						IsActive = d.IsActive,
						CreatedAt = d.CreatedAt
					})
					.ToListAsync();

				if (!discounts.Any())
					return Result<List<DiscountDto>>.Fail($"No discounts found for category {categoryId}", 404);

				return Result<List<DiscountDto>>.Ok(discounts, "Category discounts retrieved successfully", 200);
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, $"Error in GetDiscountsByCategoryAsync for categoryId: {categoryId}");
				 	_backgroundJobClient.Enqueue(()=> _errorNotificationService.SendErrorNotificationAsync(ex.Message, ex.StackTrace));
				return Result<List<DiscountDto>>.Fail("Error retrieving category discounts", 500);
			}
		}
		public async Task<Result<bool>> ActivateDiscountAsync(int id, string userId)
		{
			_logger.LogInformation($"Activating discount: {id}");
			try
			{
				var discount = await _unitOfWork.Repository<Models.Discount>().GetByIdAsync(id);
				if (discount == null)
					return Result<bool>.Fail("Discount not found", 404);

				if (discount.IsActive)
					return Result<bool>.Fail("Discount is already active", 400);

				var now = DateTime.UtcNow;

				// Optional: prevent activating expired discounts
				if (discount.EndDate < now)
					return Result<bool>.Fail("Cannot activate a discount that has already expired", 400);

				if(discount.StartDate>now)
					return Result<bool>.Fail($"Cannot activate a discount that Start Time {discount.StartDate}", 400);

				using var transaction = await _unitOfWork.BeginTransactionAsync();

				discount.IsActive = true;
				var result = _unitOfWork.Repository<Models.Discount>().Update(discount);
				if (!result)
				{
					await transaction.RollbackAsync();
					return Result<bool>.Fail("Failed to activate discount", 400);
				}

				await _adminOpreationServices.AddAdminOpreationAsync(
					 $"Activated discount '{discount.Name}' (ID: {discount.Id})",
					 Opreations.UpdateOpreation,
					userId,
					discount.Id
				);
				var productsids = await _unitOfWork.Product.GetAll().Where(p => p.DiscountId == id && p.DeletedAt == null).Select(p => p.Id).ToListAsync();
				_backgroundJobClient.Enqueue(() => _orderServices.UpdateOrderItemsForProductsAfterAddDiscountAsync(productsids, discount.DiscountPercent));
				_backgroundJobClient.Enqueue(() => _cartServices.UpdateCartItemsForProductsAfterAddDiscountAsync(productsids, discount.DiscountPercent));
				await _unitOfWork.CommitAsync();
				await transaction.CommitAsync();
				RemoveProductCaches();
				return Result<bool>.Ok(true, "Discount activated successfully", 200);
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, $"Error in ActivateDiscountAsync for id: {id}");
				_backgroundJobClient.Enqueue(() => _errorNotificationService.SendErrorNotificationAsync(ex.Message, ex.StackTrace));
				return Result<bool>.Fail("Error activating discount", 500);
			}
		}

		public async Task<Result<bool>> DeactivateDiscountAsync(int id, string userId)
		{
			_logger.LogInformation($"Deactivating discount: {id}");

			try
			{
				var discount = await _unitOfWork.Repository<Models.Discount>().GetByIdAsync(id);
				if (discount == null)
					return Result<bool>.Fail("Discount not found", 404);

				if (!discount.IsActive)
					return Result<bool>.Fail("Discount is already inactive", 400);

				using var transaction = await _unitOfWork.BeginTransactionAsync();

				discount.IsActive = false;
				var result = _unitOfWork.Repository<Models.Discount>().Update(discount);

				if (!result)
				{
					await transaction.RollbackAsync();
					return Result<bool>.Fail("Failed to deactivate discount", 400);
				}

				await _adminOpreationServices.AddAdminOpreationAsync(
					description: $"Deactivated discount '{discount.Name}' (ID: {discount.Id})",
					opreation: Opreations.UpdateOpreation,
					 userid: userId,
					itemid: discount.Id
				);

				var productsids = await _unitOfWork.Product.GetAll().Where(p => p.DiscountId == id && p.DeletedAt == null).Select(p => p.Id).ToListAsync();
				_backgroundJobClient.Enqueue(() => _cartServices.UpdateCartItemsForProductsAfterRemoveDiscountAsync(productsids));
				_backgroundJobClient.Enqueue(() => _orderServices.UpdateOrderItemsForProductsAfterRemoveDiscountAsync(productsids));

				await _unitOfWork.CommitAsync();
				await transaction.CommitAsync();
				RemoveProductCaches();

		

				return Result<bool>.Ok(true, "Discount deactivated successfully", 200);
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, $"Error in DeactivateDiscountAsync for id: {id}");
				_backgroundJobClient.Enqueue(() => _errorNotificationService.SendErrorNotificationAsync(ex.Message, ex.StackTrace));

				return Result<bool>.Fail("Error deactivating discount", 500);
			}
		}

		public async Task<Result<bool>> IsDiscountValidAsync(int id)
		{
			try
			{
				var now = DateTime.UtcNow;

				var discount = await _unitOfWork.Repository<Models.Discount>()
					.GetAll().AsNoTracking()
					.Where(d => d.Id == id && d.DeletedAt == null)
					.FirstOrDefaultAsync();

				if (discount == null)
					return Result<bool>.Ok(false, "Discount not found", 200);

				if (!discount.IsActive)
					return Result<bool>.Ok(false, "Discount is not active", 200);

				if (discount.StartDate > now)
					return Result<bool>.Ok(false, "Discount has not started yet", 200);

				if (discount.EndDate < now)
					return Result<bool>.Ok(false, "Discount has expired", 200);

				return Result<bool>.Ok(true, "Discount is valid", 200);
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, $"Error in IsDiscountValidAsync for id: {id}");
				_backgroundJobClient.Enqueue(() => _errorNotificationService.SendErrorNotificationAsync(ex.Message, ex.StackTrace));
				return Result<bool>.Fail("Error validating discount", 500);
			}
		}

		public async Task<Result<decimal>> CalculateDiscountedPriceAsync(int discountId, decimal originalPrice)
		{
			try
			{
				var discount = await _unitOfWork.Repository<Models.Discount>().GetByIdAsync(discountId);
				if (discount == null)
					return Result<decimal>.Fail("Discount not found", 404);

				var now = DateTime.UtcNow;
				if (!discount.IsActive || discount.StartDate > now || discount.EndDate < now)
					return Result<decimal>.Ok(originalPrice, "Discount not valid, returning original price", 200);

				var discountAmount = originalPrice * (discount.DiscountPercent / 100m);
				var discountedPrice = originalPrice - discountAmount;

				return Result<decimal>.Ok(discountedPrice, "Discounted price calculated successfully", 200);
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, $"Error in CalculateDiscountedPriceAsync for discountId: {discountId}");
				 	_backgroundJobClient.Enqueue(()=> _errorNotificationService.SendErrorNotificationAsync(ex.Message, ex.StackTrace));
				return Result<decimal>.Fail("Error calculating discounted price", 500);
			}
		}

		/// <summary>
		/// Updates cart item prices when a discount is activated or deactivated
		/// </summary>
		/// <param name="discountId">The ID of the discount that changed</param>
		/// <returns>Result indicating success or failure</returns>
		public async Task<Result<bool>> UpdateCartPricesOnDiscountChange(int discountId)
		{
			_logger.LogInformation($"Updating cart prices for discount: {discountId}");
			try
			{
				var discount = await _unitOfWork.Repository<Models.Discount>().GetByIdAsync(discountId);
				if (discount == null)
					return Result<bool>.Fail("Discount not found", 404);

				// Get all cart items that contain products with this discount
				var cartItemsToUpdate = await _unitOfWork.Repository<CartItem>().GetAll()
					.Include(ci => ci.Product)
					.ThenInclude(p => p.Discount)
					.Where(ci => ci.Product.DiscountId == discountId && ci.Product.DeletedAt == null)
					.ToListAsync();

				if (!cartItemsToUpdate.Any())
				{
					_logger.LogInformation($"No cart items found with discount {discountId}");
					return Result<bool>.Ok(true, "No cart items to update", 200);
				}

				using var transaction = await _unitOfWork.BeginTransactionAsync();
				var updatedCount = 0;

				foreach (var cartItem in cartItemsToUpdate)
				{
					var originalPrice = cartItem.Product.Price;
					decimal newUnitPrice;

					// Check if discount is currently valid
					var now = DateTime.UtcNow;
					var isDiscountValid = discount.IsActive && 
										 discount.StartDate <= now && 
										 discount.EndDate >= now && 
										 discount.DeletedAt == null;

					if (isDiscountValid)
					{
						// Apply discount
						var discountAmount = originalPrice * (discount.DiscountPercent / 100m);
						newUnitPrice = originalPrice - discountAmount;
						_logger.LogInformation($"Applying discount to cart item {cartItem.Id}: {originalPrice} -> {newUnitPrice}");
					}
					else
					{
						// Remove discount - use original price
						newUnitPrice = originalPrice;
						_logger.LogInformation($"Removing discount from cart item {cartItem.Id}: {cartItem.UnitPrice} -> {newUnitPrice}");
					}

					cartItem.UnitPrice = newUnitPrice;
					updatedCount++;
				}

				await _unitOfWork.CommitAsync();
				await transaction.CommitAsync();

				_logger.LogInformation($"Successfully updated {updatedCount} cart items for discount {discountId}");
				return Result<bool>.Ok(true, $"Updated {updatedCount} cart items", 200);
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, $"Error in UpdateCartPricesOnDiscountChange for discountId: {discountId}");
				_backgroundJobClient.Enqueue(() => _errorNotificationService.SendErrorNotificationAsync(ex.Message, ex.StackTrace));
				return Result<bool>.Fail("Error updating cart prices", 500);
			}
		}

	

	
	}
} 