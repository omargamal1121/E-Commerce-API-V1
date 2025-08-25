using E_Commerce.DtoModels.ProductDtos;
using E_Commerce.DtoModels.Responses;
using E_Commerce.Enums;
using E_Commerce.ErrorHnadling;
using E_Commerce.Interfaces;
using E_Commerce.Models;
using E_Commerce.Services.AdminOpreationServices;
using E_Commerce.Services.EmailServices;
using E_Commerce.UOW;
using Microsoft.EntityFrameworkCore;
using Hangfire;
using E_Commerce.Services.Cache;


using E_Commerce.Services.ProductServices;

namespace E_Commerce.Services.ProductServices
{
	public interface IProductVariantService
	{
		Task<Result<List<ProductVariantDto>>> GetProductVariantsAsync(int productId);
		Task<Result<List<ProductVariantDto>>> GetProductVariantsAsync(int productId, bool? isActive, bool? deletedOnly);
		Task<Result<ProductVariantDto>> GetVariantByIdAsync(int id);
		Task<Result<ProductVariantDto>> AddVariantAsync(int productId, CreateProductVariantDto dto, string userId);
		Task<Result<ProductVariantDto>> UpdateVariantAsync(int id, UpdateProductVariantDto dto, string userId);
		Task<Result<bool>> DeleteVariantAsync(int id, string userId);
		Task<Result<bool>> UpdateVariantQuantityAsync(int id, int newQuantity, string userId);
		public  Task<Result<List<ProductVariantDto>>> GetVariantsBySearchAsync(int productId, string? color = null, int? Length = null, int? wist = null, VariantSize? size = null, bool? isActive = null, bool? deletedOnly = null);
		Task<Result<bool>> ActivateVariantAsync(int id, string userId);
		Task<Result<bool>> DeactivateVariantAsync(int id, string userId);
		Task<Result<bool>> AddVariantQuantityAsync(int id, int addQuantity, string userId);
		Task<Result<bool>> RemoveVariantQuantityAsync(int id, int removeQuantity, string userId);
		Task<Result<bool>> RestoreVariantAsync(int id, string userId);
		public Task<Result<bool>> RemoveQuntityAfterOrder(int id, int quantity);
			public  Task<Result<bool>> AddQuntityAfterRestoreOrder(int id, int addQuantity);
	}

	public class ProductVariantService : IProductVariantService
	{
		private readonly IUnitOfWork _unitOfWork;
		private readonly ILogger<ProductVariantService> _logger;
		private readonly IAdminOpreationServices _adminOpreationServices;
		private readonly IErrorNotificationService _errorNotificationService;
		private readonly ICacheManager _cacheManager;
		private readonly IBackgroundJobClient _backgroundJobClient;
		private readonly IProductCatalogService _productCatalogService;

		public ProductVariantService(
			IUnitOfWork unitOfWork,
			ILogger<ProductVariantService> logger,
			IAdminOpreationServices adminOpreationServices,
			IErrorNotificationService errorNotificationService,
			ICacheManager cacheManager,
			IBackgroundJobClient backgroundJobClient,
			IProductCatalogService productCatalogService)
		{
			_unitOfWork = unitOfWork;
			_logger = logger;
			_adminOpreationServices = adminOpreationServices;
			_errorNotificationService = errorNotificationService;
			_cacheManager = cacheManager;
			_backgroundJobClient = backgroundJobClient;
			_productCatalogService = productCatalogService;
		}
		private string GetVariantCacheKey(int id) => $"variant:{id}";
		private string GetProductVariantsCacheKey(int productId) => $"product:{productId}:variants";
		private string GetProductCacheTag(int productId) => $"product:{productId}";
	

		private const string PRODUCT_WITH_VARIANT_TAG = "productwithvariantdata";
		private const string CACHE_TAG_PRODUCT_SEARCH = "product_search";
		private const string VARIANT_DATA_TAG = "variantdata";
		private static readonly string[] PRODUCT_CACHE_TAGS = new[] { PRODUCT_WITH_VARIANT_TAG, CACHE_TAG_PRODUCT_SEARCH, VARIANT_DATA_TAG };

		private void RemoveProductCachesAsync()
		{
			_backgroundJobClient.Enqueue(() => _cacheManager.RemoveByTagsAsync(PRODUCT_CACHE_TAGS));
		}

		public async Task<Result<bool>> AddQuntityAfterRestoreOrder(int id, int addQuantity)
		{
			_logger.LogInformation($"Adding quantity for variant: {id}");
			using var transaction = await _unitOfWork.BeginTransactionAsync();
			try
			{
				if (addQuantity <= 0)
					return Result<bool>.Fail("Add quantity must be positive", 400);

				var variant = await _unitOfWork.Repository<ProductVariant>().GetByIdAsync(id);
				if (variant == null)
					return Result<bool>.Fail("Variant not found", 404);

				variant.Quantity += addQuantity;

				 RemoveProductCachesAsync();
				_productCatalogService.UpdateProductQuantity(variant.ProductId);
				await _unitOfWork.CommitAsync();
				await transaction.CommitAsync();


				_logger.LogInformation($"Quantity for variant {id} restored to {variant.Quantity}");
				return Result<bool>.Ok(true);
			}
			catch (Exception ex)
			{
				await transaction.RollbackAsync();
				_logger.LogError(ex, $"Error in AddQuntityAfterRestoreOrder for id: {id}");
				_backgroundJobClient.Enqueue(() =>
					_errorNotificationService.SendErrorNotificationAsync(ex.Message, ex.StackTrace)
				);
				return Result<bool>.Fail("Error adding quantity", 500);
			}
		}

		public async Task<Result<bool>> RemoveQuntityAfterOrder(int id, int quantity)
		{
			_logger.LogInformation($"Removing {quantity} from variant: {id}");
			using var transaction = await _unitOfWork.BeginTransactionAsync();
			try
			{
				if (quantity <= 0)
					return Result<bool>.Fail("Remove quantity must be positive", 400);

				var variant = await _unitOfWork.Repository<ProductVariant>().GetByIdAsync(id);
				if (variant == null)
				{
					_logger.LogWarning($"Variant {id} not found");
					return Result<bool>.Fail("Variant not found", 404);
				}

				if (variant.Quantity < quantity)
					return Result<bool>.Fail("Not enough quantity to remove", 400);

				variant.Quantity -= quantity;

				if (variant.Quantity == 0)
				{
					variant.IsActive = false;
					_backgroundJobClient.Enqueue(() =>
						CheckAndDeactivateProductIfAllVariantsInactiveOrZeroAsync(variant.ProductId)
					);
				}
				_productCatalogService.UpdateProductQuantity(variant.ProductId);

				await _unitOfWork.CommitAsync();
				await transaction.CommitAsync();

				return Result<bool>.Ok(true);
			}
			catch (Exception ex)
			{
				await transaction.RollbackAsync();
				_logger.LogError(ex, $"Error in RemoveQuntityAfterOrder for id: {id}");
				_backgroundJobClient.Enqueue(() =>
					_errorNotificationService.SendErrorNotificationAsync(ex.Message, ex.StackTrace)
				);
				return Result<bool>.Fail("Error removing quantity", 500);
			}
		}

		private async Task CheckAndDeactivateProductIfAllVariantsInactiveOrZeroAsync(int productId)
		{
			var variants = await _unitOfWork.Repository<ProductVariant>().GetAll()
				.Where(v => v.ProductId == productId && v.DeletedAt == null)
				.ToListAsync();
			if (variants.Count == 0 || variants.All(v => !v.IsActive || v.Quantity == 0))
			{
				var product = await _unitOfWork.Product.GetByIdAsync(productId);
				if (product != null && product.IsActive)
				{
					
					await _productCatalogService.DeactivateProductAsync(productId, "system");
				}
			}
		}

	

		public async Task<Result<List<ProductVariantDto>>> GetProductVariantsAsync(int id)
		{
			var cacheKey = GetProductVariantsCacheKey(id);
			var cached = await _cacheManager.GetAsync<List<ProductVariantDto>>(cacheKey);
			if (cached != null)
				return Result<List<ProductVariantDto>>.Ok(cached, "Product variants retrieved from cache", 200);

			var result = await GetProductVariantsAsync(id, null, null);
			if (result.Success)
			{
				_backgroundJobClient.Enqueue(() => _cacheManager.SetAsync(cacheKey, result.Data, null, new[] { GetProductCacheTag(id), VARIANT_DATA_TAG }));
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

				var variants = await query
					.Select(v => new ProductVariantDto
					{
						Id = v.Id,
						Color = v.Color,
						Size = v.Size,
						Waist = v.Waist,
						Length = v.Length,
						Quantity = v.Quantity,
						ProductId = v.ProductId,
						IsActive = v.IsActive,
						CreatedAt = v.CreatedAt,
						DeletedAt = v.DeletedAt,
						ModifiedAt = v.ModifiedAt


					})
					.ToListAsync();

				if (!variants.Any())
					return Result<List<ProductVariantDto>>.Fail("No variants found for this product", 404);

				return Result<List<ProductVariantDto>>.Ok(variants, "Product variants retrieved successfully", 200);
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, $"Error in GetProductVariantsAsync for productId: {productId}");
				 	_backgroundJobClient.Enqueue(()=> _errorNotificationService.SendErrorNotificationAsync(ex.Message, ex.StackTrace));
				return Result<List<ProductVariantDto>>.Fail("Error retrieving product variants", 500);
			}
		}

		public async Task<Result<ProductVariantDto>> GetVariantByIdAsync(int id)
		{
			_logger.LogInformation($"Getting variant by id: {id}");
			var cacheKey = GetVariantCacheKey(id);
			var cached = await _cacheManager.GetAsync<ProductVariantDto>(cacheKey);
			if (cached != null)
			{
				_logger.LogInformation($"Retrieved variant {id} from cache");
				return Result<ProductVariantDto>.Ok(cached, "Variant retrieved from cache", 200);
			}

			try
			{
				var variant = await _unitOfWork.ProductVariant.GetVariantById(id);

				if (variant == null||variant.DeletedAt!=null)
				{
					_logger.LogWarning($"Variant {id} not found or is deleted");
					return Result<ProductVariantDto>.Fail("Variant not found", 404);
				}
				var variantdto = new ProductVariantDto
				{
					Color = variant.Color,

					Length=variant.Length,
					CreatedAt=variant.CreatedAt,
					DeletedAt = variant.DeletedAt,
					Id=variant.Id,
					 IsActive=variant.IsActive,
					  ModifiedAt=variant.ModifiedAt,
					  Quantity=variant.Quantity,
					  Size=variant.Size,
					  Waist=variant.Waist

				};
				_logger.LogInformation($"Caching variant {id} data");
				_backgroundJobClient.Enqueue(() => _cacheManager.SetAsync(cacheKey, variantdto, null, new[] { GetProductCacheTag(variant.ProductId), VARIANT_DATA_TAG }));
				return Result<ProductVariantDto>.Ok(variantdto, "Variant retrieved successfully", 200);
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, $"Error in GetVariantByIdAsync for id: {id}");
				 	_backgroundJobClient.Enqueue(()=> _errorNotificationService.SendErrorNotificationAsync(ex.Message, ex.StackTrace));
				return Result<ProductVariantDto>.Fail("Error retrieving variant", 500);
			}
		}

		public async Task<Result<ProductVariantDto>> AddVariantAsync(int productId, CreateProductVariantDto dto, string userId)
		{
			_logger.LogInformation($"Adding variant to product: {productId}");
			
			var product = await _unitOfWork.Product.IsExsistAsync(productId);
				if (!product)
					return Result<ProductVariantDto>.Fail("Product not found", 404);

				if (string.IsNullOrEmpty(dto.Color) || dto.Size == null)
					return Result<ProductVariantDto>.Fail("Color and Size are required", 400);

				if (dto.Quantity < 0)
					return Result<ProductVariantDto>.Fail("Quantity cannot be negative", 400);

			_logger.LogInformation($"Checking if variant with color={dto.Color}, size={dto.Size}, waist={dto.Waist}, length={dto.Length} already exists for product {productId}");
			var existingVariant = await _unitOfWork.ProductVariant.IsExsistBySizeandColor(productId, dto.Color, dto.Size, dto.Waist, dto.Length);
				
			if (existingVariant)
			{
				_logger.LogWarning($"Attempt to add duplicate variant for product {productId} with color={dto.Color}, size={dto.Size}");
				return Result<ProductVariantDto>.Fail("Variant with this color , size , waist and length already exists", 400);
			}

				using var transaction = await _unitOfWork.BeginTransactionAsync();
			try
			{
				var variant = new ProductVariant
				{
					ProductId = productId,
					Color = dto.Color,
					Length= dto.Length,
					Size = dto.Size,
					Quantity = dto.Quantity,
					IsActive = true ,
					Waist= dto.Waist
					
				};

				var result = await _unitOfWork.Repository<ProductVariant>().CreateAsync(variant);
				if (result == null)
				{
					await transaction.RollbackAsync();
					return Result<ProductVariantDto>.Fail("Failed to add variant", 400);
				}
				_logger.LogInformation($"Updating product {productId} quantity after adding variant");
				_backgroundJobClient.Enqueue(()=> _productCatalogService.UpdateProductQuantity(productId));

				_logger.LogInformation($"Recording admin operation for adding variant to product {productId} by user {userId}");
			var isadded = await _adminOpreationServices.AddAdminOpreationAsync(
					$"Add Variant to Product {productId}",
					Opreations.AddOpreation,
					userId,
					productId
				);
				if(isadded==null)
				{
					_logger.LogError($"Failed to record admin operation for adding variant to product {productId}");
					return Result<ProductVariantDto>.Fail("Error adding variant", 500);
				}

				_logger.LogInformation($"Committing transaction for adding variant to product {productId}");
				await _unitOfWork.CommitAsync();
				await transaction.CommitAsync();
				_logger.LogInformation($"Successfully added variant to product {productId}");

				var variantDto = new ProductVariantDto
				{
					Id = variant.Id,
					Color = variant.Color,
					Size = variant.Size,
					Waist = variant.Waist,
					Length = variant.Length,
					Quantity = variant.Quantity,
					ProductId = variant.ProductId,
					CreatedAt=variant.CreatedAt,
					DeletedAt = variant.DeletedAt,
					IsActive= variant.IsActive,
					


				};

				_logger.LogInformation($"Removing product caches after adding variant to product {productId}");
				RemoveProductCachesAsync();

				_logger.LogInformation($"Successfully completed adding variant (ID: {variant.Id}) to product {productId}");
				return Result<ProductVariantDto>.Ok(variantDto, "Variant added successfully", 201);
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, $"Error in AddVariantAsync for productId: {productId}. Rolling back transaction.");
				await transaction.RollbackAsync();
				_logger.LogInformation($"Transaction rolled back for adding variant to product {productId}");
				_backgroundJobClient.Enqueue(()=> _errorNotificationService.SendErrorNotificationAsync(ex.Message, ex.StackTrace));
				return Result<ProductVariantDto>.Fail("Error adding variant", 500);
			}
		}

		public async Task<Result<ProductVariantDto>> UpdateVariantAsync(int id, UpdateProductVariantDto dto, string userId)
		{
			_logger.LogInformation($"Updating variant: {id}");
			using var transaction = await _unitOfWork.BeginTransactionAsync();
			try
			{
				var variant = await _unitOfWork.ProductVariant.GetByIdAsync(id);
				if (variant == null)
					return Result<ProductVariantDto>.Fail("Variant not found", 404);


				_logger.LogInformation($"Checking if variant with color={dto.Color}, size={dto.Size}, waist={dto.Waist}, length={dto.Length} already exists for product {variant.ProductId}");
			var isexsist = await _unitOfWork.ProductVariant.IsExsistBySizeandColor(variant.ProductId, dto.Color, dto.Size, dto.Waist, dto.Length);
			if(isexsist)
			{
				_logger.LogWarning($"Attempt to update variant {id} with duplicate attributes for product {variant.ProductId}");

					return Result<ProductVariantDto>.Fail("Thier's Varinat with this data ");

				}


				_logger.LogInformation($"Starting to update variant {id} properties");
				string updates = string.Empty;


				if (!string.IsNullOrEmpty(dto.Color)&& dto.Color != variant.Color)
				{
					_logger.LogInformation($"Updating variant {id} color from {variant.Color} to {dto.Color}");
					updates += $"from {variant.Color} to {dto.Color}";
					variant.Color = dto.Color;
				}
				if (dto.Size != null && dto.Size != variant.Size){
					_logger.LogInformation($"Updating variant {id} size from {variant.Size} to {dto.Size}");
					updates += $"from {variant.Size} to {dto.Size}";
					variant.Size = dto.Size;
				}
				if (dto.Waist.HasValue && dto.Waist != variant.Waist){
					_logger.LogInformation($"Updating variant {id} waist from {variant.Waist} to {dto.Waist}");
					updates+=$"from {variant.Waist} to {dto.Waist}";
					variant.Waist = dto.Waist;
				}
				if (dto.Length.HasValue && dto.Length != variant.Length)
				{
					_logger.LogInformation($"Updating variant {id} length from {variant.Length} to {dto.Length}");
					updates += $"from {variant.Length} to {dto.Length}";
					variant.Length = dto.Length;
				}
				
			
				// Log admin operation
				_logger.LogInformation($"Recording admin operation for updating variant {id} by user {userId}");
			var isadded = await _adminOpreationServices.AddAdminOpreationAsync(
					$"Update Variant {id}"+updates,
					Opreations.UpdateOpreation,
					userId,
					variant.ProductId
				);

				if(isadded==null)
				{
					_logger.LogError($"Failed to record admin operation for updating variant {id}");
					return Result<ProductVariantDto>.Fail("Error updating variant", 500);
				}

				_logger.LogInformation($"Committing transaction for updating variant {id}");
				await _unitOfWork.CommitAsync();
				await transaction.CommitAsync();
				_logger.LogInformation($"Successfully updated variant {id}");

				_logger.LogInformation($"Removing product caches after updating variant {id}");
				RemoveProductCachesAsync();

				var variantDto = new ProductVariantDto
				{
					Id = variant.Id,
					Color = variant.Color,
					Size = variant.Size,
					Waist = variant.Waist,
					Length = variant.Length,
					Quantity = variant.Quantity,
					ProductId = variant.ProductId,
					CreatedAt = variant.CreatedAt,
					DeletedAt = variant.DeletedAt,
					ModifiedAt = variant.ModifiedAt
				};

				return Result<ProductVariantDto>.Ok(variantDto, "Variant updated successfully", 200);
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, $"Error in UpdateVariantAsync for id: {id}. Rolling back transaction.");
				await transaction.RollbackAsync();
				_logger.LogInformation($"Transaction rolled back for updating variant {id}");
				_backgroundJobClient.Enqueue(()=> _errorNotificationService.SendErrorNotificationAsync(ex.Message, ex.StackTrace));
				return Result<ProductVariantDto>.Fail("Error updating variant", 500);
			}
		}

		public async Task<Result<bool>> DeleteVariantAsync(int id, string userId)
		{
			_logger.LogInformation($"Deleting variant: {id}");

			using var transaction = await _unitOfWork.BeginTransactionAsync();
			try
			{
				var variant = await _unitOfWork.Repository<ProductVariant>().GetByIdAsync(id);
				if (variant == null)
					return Result<bool>.Fail("Variant not found", 404);


				var isinorders = await _unitOfWork.Repository<OrderItem>().GetAll().AnyAsync(i => i.ProductVariantId == id &&
				(i.Order.Status != OrderStatus.CancelledByAdmin && i.Order.Status != OrderStatus.CancelledByUser &&
				i.Order.Status != OrderStatus.Complete)
				);
				if(isinorders)
				{
					return Result<bool>.Fail("Can't remove this becouse it in order", 400);
				}

				_logger.LogInformation($"Attempting to soft delete variant {id}");
				var result = await _unitOfWork.Repository<ProductVariant>().SoftDeleteAsync(id);
				if (!result)
				{
					_logger.LogWarning($"Failed to soft delete variant {id}. Rolling back transaction.");
					await transaction.RollbackAsync();
					return Result<bool>.Fail("Failed to delete variant", 400);
				}
				_logger.LogInformation($"Successfully soft deleted variant {id}");

				

				_logger.LogInformation($"Updating product {variant.ProductId} quantity after deleting variant {id}");
				_productCatalogService.UpdateProductQuantity(variant.ProductId);
				
				_logger.LogInformation($"Recording admin operation for deleting variant {id} by user {userId}");
				var isAdded = await _adminOpreationServices.AddAdminOpreationAsync(
					$"Delete Variant {id}",
					Opreations.DeleteOpreation,
					userId,
					variant.ProductId
				);
				
				if(isAdded == null)
				{
					_logger.LogError($"Failed to record admin operation for deleting variant {id}");
				}

				await _unitOfWork.CommitAsync();
				await transaction.CommitAsync();

				_logger.LogInformation($"Removing product caches after deleting variant {id}");
				RemoveProductCachesAsync();
				
				_logger.LogInformation($"Checking if product {variant.ProductId} should be deactivated after variant deletion");
				await CheckAndDeactivateProductIfAllVariantsInactiveOrZeroAsync(variant.ProductId);
				
				_logger.LogInformation($"Successfully completed all operations for deleting variant {id}");
				return Result<bool>.Ok(true, "Variant deleted successfully", 200);
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, $"Error in DeleteVariantAsync for id: {id}. Rolling back transaction.");
				await transaction.RollbackAsync();
				_logger.LogInformation($"Transaction rolled back for deleting variant {id}");
				_backgroundJobClient.Enqueue(()=> _errorNotificationService.SendErrorNotificationAsync(ex.Message, ex.StackTrace));
				return Result<bool>.Fail("Error deleting variant", 500);
			}
		}

		public async Task<Result<bool>> UpdateVariantQuantityAsync(int id, int newQuantity, string userId)
		{
			_logger.LogInformation($"Updating quantity for variant: {id}");
			using var transaction = await _unitOfWork.BeginTransactionAsync();
			try
			{
				if (newQuantity < 0)
					return Result<bool>.Fail("Quantity cannot be negative", 400);

				var variant = await _unitOfWork.ProductVariant.GetByIdAsync(id);
				if (variant==null)
					return Result<bool>.Fail("Variant not found", 404);


			_logger.LogInformation($"Attempting to update variant {id} quantity from {variant.Quantity} to {newQuantity}");
			var result = await _unitOfWork.ProductVariant.UpdateVariantQuantityAsync(id, newQuantity);
			if(!result)
				{
					_logger.LogWarning($"Failed to update quantity for variant {id}");
					return Result<bool>.Fail("Error updating quantity", 500);
				}
			_logger.LogInformation($"Successfully updated variant {id} quantity to {newQuantity}");


				_logger.LogInformation($"Recording admin operation for updating quantity of variant {id} by user {userId}");
				var isadded = await _adminOpreationServices.AddAdminOpreationAsync(
					$"Update Quantity for Variant {id}",
					Opreations.UpdateOpreation,
					userId,
					variant.ProductId
				);

				if (isadded == null)
				{
					_logger.LogError($"Failed to record admin operation for updating quantity of variant {id}");
					return Result<bool>.Fail("Error updating quantity", 500);


				}
				_logger.LogInformation($"Updating product {variant.ProductId} quantity after updating variant {id} quantity");
				_productCatalogService.UpdateProductQuantity(variant.ProductId);
				
				_logger.LogInformation($"Removing product caches after updating variant {id} quantity");
				RemoveProductCachesAsync();
				
				_logger.LogInformation($"Committing transaction for updating variant {id} quantity");
				await _unitOfWork.CommitAsync();
				await transaction.CommitAsync();
				
				_logger.LogInformation($"Checking if product {variant.ProductId} should be deactivated after quantity update");
				await CheckAndDeactivateProductIfAllVariantsInactiveOrZeroAsync(variant.ProductId);
				return Result<bool>.Ok(true, "Quantity updated successfully", 200);
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, $"Error in UpdateVariantQuantityAsync for id: {id}. Rolling back transaction.");
				await transaction.RollbackAsync();
				_logger.LogInformation($"Transaction rolled back for updating variant {id} quantity");
				_backgroundJobClient.Enqueue(()=> _errorNotificationService.SendErrorNotificationAsync(ex.Message, ex.StackTrace));
				return Result<bool>.Fail("Error updating quantity", 500);
			}
		}

		public async Task<Result<List<ProductVariantDto>>> GetVariantsBySearchAsync(int productId, string?color = null, int? Length = null, int? wist = null, VariantSize? size = null, bool? isActive = null, bool? deletedOnly = null)
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

				var variants = await query
					.Select(v => new ProductVariantDto
					{
						Id = v.Id,
						Color = v.Color,
						Size = v.Size,
						Waist = v.Waist,
						Length = v.Length,
						Quantity = v.Quantity,
						ProductId = v.ProductId,
						IsActive= v.IsActive,
						CreatedAt = v.CreatedAt,
						DeletedAt = v.DeletedAt,
						ModifiedAt = v.ModifiedAt

					})
					.ToListAsync();
				if (!variants.Any())
				{
					_logger.LogWarning($"No variants found matching the search criteria for product {productId}");
					return Result<List<ProductVariantDto>>.Fail("No variants found matching the search criteria", 404);
				}

				// Store results in cache
				_logger.LogInformation($"Caching search results for product {productId}");
				_backgroundJobClient.Enqueue(() => _cacheManager.SetAsync(cacheKey, variants, null, new[] { GetProductCacheTag(productId), VARIANT_DATA_TAG }));

				_logger.LogInformation($"Successfully retrieved {variants.Count} variants for product {productId}");
				return Result<List<ProductVariantDto>>.Ok(variants, "Variants retrieved successfully", 200);
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, $"Error in GetVariantsBySearchAsync for productId: {productId}");
				 	_backgroundJobClient.Enqueue(()=> _errorNotificationService.SendErrorNotificationAsync(ex.Message, ex.StackTrace));
				return Result<List<ProductVariantDto>>.Fail("Error retrieving variants by search", 500);
			}
		}

		public async Task<Result<bool>> ActivateVariantAsync(int id, string userId)
		{
			_logger.LogInformation($"Activating variant: {id}");
			using var transaction = await _unitOfWork.BeginTransactionAsync();
			try
			{
				_logger.LogInformation($"Checking if variant {id} exists and is not deleted");
				var varaintinfo = await _unitOfWork.ProductVariant.GetAll().Where(v => v.Id == id).Select(v => new { 
				 hasquntity=v.Quantity > 0,
				 isdeleted=v.DeletedAt!=null,
				 hassize= v.Size!=null,
				 haslengthandwaist=v.Length!=0&&v.Waist!=0,
				 productid=v.ProductId
				}).FirstOrDefaultAsync();

				var variant = await _unitOfWork.ProductVariant.IsExsistAsync(id);
				if (varaintinfo == null|| varaintinfo.isdeleted)
				{
					_logger.LogWarning($"Variant {id} not found or is deleted, cannot activate");
					return Result<bool>.Fail("Variant not found", 404);
				}


			
				_logger.LogInformation($"Validating variant {id} has required properties for activation");
				if (!(varaintinfo.hasquntity  && (varaintinfo.haslengthandwaist || varaintinfo.hassize)))
				{
					_logger.LogWarning($"Variant {id} does not meet activation requirements: hasQuantity={varaintinfo.hasquntity}, hasSize={varaintinfo.hassize}, hasLengthAndWaist={varaintinfo.haslengthandwaist}");
					return Result<bool>.Fail("To activate, variant must have (length and waist) or size, and quantity > 0", 400);
				}

				_logger.LogInformation($"Attempting to activate variant {id}");
				var result = await  _unitOfWork.ProductVariant.ActiveVaraintAsync(id);
				if (!result)
				{
					_logger.LogWarning($"Failed to activate variant {id}");
					await transaction.RollbackAsync();
					_logger.LogInformation($"Transaction rolled back for activating variant {id}");
					return Result<bool>.Fail("Failed to activate variant", 400);
				}
				_logger.LogInformation($"Successfully activated variant {id}");
				_logger.LogInformation($"Updating product {varaintinfo.productid} quantity after activating variant {id}");
				_productCatalogService.UpdateProductQuantity(varaintinfo.productid);

				_logger.LogInformation($"Recording admin operation for activating variant {id} by user {userId}");
				var isAdded = await _adminOpreationServices.AddAdminOpreationAsync(
					$"Activate Variant {id}",
					Opreations.UpdateOpreation,
					userId,
					id
				);

				if(isAdded == null)
				{
					_logger.LogError($"Failed to record admin operation for activating variant {id}");
				}

				_logger.LogInformation($"Committing unit of work for activating variant {id}");
				await _unitOfWork.CommitAsync();

				_logger.LogInformation($"Committing transaction for activating variant {id}");
				await transaction.CommitAsync();
			
				_logger.LogInformation($"Removing product caches after activating variant {id}");
				RemoveProductCachesAsync();


				_logger.LogInformation($"Successfully completed all operations for activating variant {id}");
				return Result<bool>.Ok(true, "Variant activated", 200);
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, $"Error in ActivateVariantAsync for id: {id}. Rolling back transaction.");
				await transaction.RollbackAsync();
				_logger.LogInformation($"Transaction rolled back for activating variant {id}");
				_backgroundJobClient.Enqueue(()=> _errorNotificationService.SendErrorNotificationAsync(ex.Message, ex.StackTrace));
				return Result<bool>.Fail("Error activating variant", 500);
			}
		}

		public async Task<Result<bool>> DeactivateVariantAsync(int id, string userId)
		{
			_logger.LogInformation($"Deactivating variant: {id}");
			using var transaction = await _unitOfWork.BeginTransactionAsync();
			try
			{
				var varaintinfo = await _unitOfWork.ProductVariant.GetAll().Where(v => v.Id == id).Select(v => new
				{
					hasquntity = v.Quantity > 0,
					isdeleted = v.DeletedAt != null,
					hassize = v.Size != null,
					haslengthandwaist = v.Length != 0 && v.Waist != 0,
					productid = v.ProductId
				}).FirstOrDefaultAsync();

				
				if (varaintinfo == null|| varaintinfo.isdeleted)
					return Result<bool>.Fail("Variant not found", 404);

				_logger.LogInformation($"Attempting to deactivate variant {id}");
				var result = await _unitOfWork.ProductVariant.DeactiveVaraintAsync(id);
				if(!result)
				{
					_logger.LogWarning($"Failed to deactivate variant {id}");
					return Result<bool>.Fail("Error deactivating variant", 500);
				}
				_logger.LogInformation($"Successfully deactivated variant {id}");
				

				_productCatalogService.UpdateProductQuantity(varaintinfo.productid);
		
				await _adminOpreationServices.AddAdminOpreationAsync(
					$"Deactivate Variant {id}",
					Opreations.UpdateOpreation,
					userId,
					id
				);

				await _unitOfWork.CommitAsync();
				await transaction.CommitAsync();

				RemoveProductCachesAsync();
				await CheckAndDeactivateProductIfAllVariantsInactiveOrZeroAsync(varaintinfo.productid);


				return Result<bool>.Ok(true, "Variant deactivated", 200);
			}
			catch (Exception ex)
			{
				await transaction.RollbackAsync();
				_logger.LogError(ex, $"Error in DeactivateVariantAsync for id: {id}");
				 	_backgroundJobClient.Enqueue(()=> _errorNotificationService.SendErrorNotificationAsync(ex.Message, ex.StackTrace));
				return Result<bool>.Fail("Error deactivating variant", 500);
			}
		}

		public async Task<Result<bool>> AddVariantQuantityAsync(int id, int addQuantity, string userId)
		{
			_logger.LogInformation($"Adding quantity for variant: {id}");
			using var transaction = await _unitOfWork.BeginTransactionAsync();
			try
			{
				if (addQuantity <= 0)
					return Result<bool>.Fail("Add quantity must be positive", 400);

				var variant = await _unitOfWork.Repository<ProductVariant>().GetByIdAsync(id);
				if (variant == null)
					return Result<bool>.Fail("Variant not found", 404);


				var newquantity= variant.Quantity + addQuantity;

				var result = await _unitOfWork.ProductVariant.UpdateVariantQuantityAsync(id,newquantity);
				if (!result)
				{
					await transaction.RollbackAsync();
					return Result<bool>.Fail("Failed to add quantity", 400);
				}

				await _adminOpreationServices.AddAdminOpreationAsync(
					$"Add Quantity for Variant {id}",
					Opreations.UpdateOpreation,
					userId,
					variant.ProductId
				);

			
					_productCatalogService.UpdateProductQuantity(variant.ProductId);
				
				
				RemoveProductCachesAsync();
				await _unitOfWork.CommitAsync();
				await transaction.CommitAsync();

				return Result<bool>.Ok(true, "Quantity added successfully", 200);
			}
			catch (Exception ex)
			{
				await transaction.RollbackAsync();
				_logger.LogError(ex, $"Error in AddVariantQuantityAsync for id: {id}");
				 	_backgroundJobClient.Enqueue(()=> _errorNotificationService.SendErrorNotificationAsync(ex.Message, ex.StackTrace));
				return Result<bool>.Fail("Error adding quantity", 500);
			}
		}

		public async Task<Result<bool>> RemoveVariantQuantityAsync(int id, int removeQuantity, string userId)
		{
			_logger.LogInformation($"Removing quantity for variant: {id}");
			using var transaction = await _unitOfWork.BeginTransactionAsync();
			try
			{
				var isupdate = await RemoveQuntityAfterOrder(id, removeQuantity);

				if(!isupdate.Success)
				{
					return Result<bool>.Fail(isupdate.Message);

				}	
			

				_logger.LogInformation($"Recording admin operation for removing quantity from variant {id} by user {userId}");
				var isAdded = await _adminOpreationServices.AddAdminOpreationAsync(
					$"Remove Quantity for Variant {id} RemoveQuantity {removeQuantity} ",
					Opreations.UpdateOpreation,
					userId,
					id
				);

				if(isAdded == null)
				{
					_logger.LogError($"Failed to record admin operation for removing quantity from variant {id}");
					 await transaction.RollbackAsync();
				}

		
			
		
				_logger.LogInformation($"Committing transaction for removing quantity from variant {id}");
				await _unitOfWork.CommitAsync();
				await transaction.CommitAsync();
				
				_logger.LogInformation($"Removing product caches after removing quantity from variant {id}");
				RemoveProductCachesAsync();
				return Result<bool>.Ok(true, "Quantity removed successfully", 200);
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, $"Error in RemoveVariantQuantityAsync for id: {id}. Rolling back transaction.");
				await transaction.RollbackAsync();
				_logger.LogInformation($"Transaction rolled back for removing quantity from variant {id}");
				_backgroundJobClient.Enqueue(()=> _errorNotificationService.SendErrorNotificationAsync(ex.Message, ex.StackTrace));
				return Result<bool>.Fail("Error removing quantity", 500);
			}
		}

		public async Task<Result<bool>> RestoreVariantAsync(int id, string userId)
		{
			_logger.LogInformation($"Restoring variant: {id}");
			using var transaction = await _unitOfWork.BeginTransactionAsync();
			try
			{
				_logger.LogInformation($"Checking if variant {id} exists and is deleted");
			var variantinfo = await _unitOfWork.ProductVariant.GetAll().Where(i=>i.Id==id).Select(v => new
			{
				productid=v.ProductId,
				isdeleted=v.DeletedAt!=null
			}).FirstOrDefaultAsync();
		
			if(variantinfo == null || !variantinfo.isdeleted)
			{
				_logger.LogWarning($"Variant {id} not found or is not deleted, cannot restore");
					return Result<bool>.Fail("Variant not found or not deleted", 404);
			}
				_logger.LogInformation($"Attempting to restore deleted variant {id}");
				var restoredvaraint = await _unitOfWork.ProductVariant.RestoreAsync(id);
				if (!restoredvaraint)
				{
					_logger.LogWarning($"Failed to restore variant {id}");
					return Result<bool>.Fail("Failed to restore variant", 500);
				}
				_logger.LogInformation($"Successfully restored variant {id}");


			
				_logger.LogInformation($"Recording admin operation for restoring variant {id} by user {userId}");
				var isAdded = await _adminOpreationServices.AddAdminOpreationAsync(
					$"Restore Variant {id}",
					Opreations.UpdateOpreation,
					userId,
					variantinfo.productid
				);

				if(isAdded == null)
				{
					_logger.LogError($"Failed to record admin operation for restoring variant {id}");
				}

				_logger.LogInformation($"Updating product {variantinfo.productid} quantity after restoring variant {id}");
				_productCatalogService.UpdateProductQuantity(variantinfo.productid);
				
				_logger.LogInformation($"Committing transaction for restoring variant {id}");
				await _unitOfWork.CommitAsync();
				
				_logger.LogInformation($"Removing product caches after restoring variant {id}");
				RemoveProductCachesAsync();

				_logger.LogInformation($"Committing transaction for restoring variant {id}");
				await transaction.CommitAsync();
				_logger.LogInformation($"Successfully completed all operations for restoring variant {id}");
				return Result<bool>.Ok(true, "Variant restored successfully", 200);
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, $"Error in RestoreVariantAsync for id: {id}. Rolling back transaction.");
				await transaction.RollbackAsync();
				_logger.LogInformation($"Transaction rolled back for restoring variant {id}");
				_backgroundJobClient.Enqueue(()=> _errorNotificationService.SendErrorNotificationAsync(ex.Message, ex.StackTrace));
				return Result<bool>.Fail("Error restoring variant", 500);
			}
		}
	}
}