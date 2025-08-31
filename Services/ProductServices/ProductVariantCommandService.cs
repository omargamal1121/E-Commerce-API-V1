using E_Commerce.DtoModels.ProductDtos;
using E_Commerce.DtoModels.Responses;
using E_Commerce.Enums;
using E_Commerce.ErrorHnadling;
using E_Commerce.Interfaces;
using E_Commerce.Models;
using E_Commerce.UOW;
using Microsoft.EntityFrameworkCore;
using Hangfire;
using E_Commerce.Services.EmailServices;
using E_Commerce.Services.AdminOperationServices;

namespace E_Commerce.Services.ProductServices
{
    public class ProductVariantCommandService : IProductVariantCommandService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly ILogger<ProductVariantCommandService> _logger;
        private readonly IAdminOpreationServices _adminOpreationServices;
        private readonly IErrorNotificationService _errorNotificationService;
        private readonly IBackgroundJobClient _backgroundJobClient;
        private readonly IProductCatalogService _productCatalogService;
        private readonly IProductVariantCacheHelper _cacheHelper;
        private readonly IProductVariantMapper _mapper;

        public ProductVariantCommandService(
            IUnitOfWork unitOfWork,
            ILogger<ProductVariantCommandService> logger,
            IAdminOpreationServices adminOpreationServices,
            IErrorNotificationService errorNotificationService,
            IBackgroundJobClient backgroundJobClient,
            IProductCatalogService productCatalogService,
            IProductVariantCacheHelper cacheHelper,
            IProductVariantMapper mapper)
        {
            _unitOfWork = unitOfWork;
            _logger = logger;
            _adminOpreationServices = adminOpreationServices;
            _errorNotificationService = errorNotificationService;
            _backgroundJobClient = backgroundJobClient;
            _productCatalogService = productCatalogService;
            _cacheHelper = cacheHelper;
            _mapper = mapper;
        }

        #region Quantity Management Methods
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

                await _unitOfWork.CommitAsync();
                await transaction.CommitAsync();
                // Update cache and product catalog
                _cacheHelper.RemoveProductCachesAsync();

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
				_backgroundJobClient.Enqueue(() => _productCatalogService.UpdateProductQuantity(variant.ProductId));
				_cacheHelper.RemoveProductCachesAsync();
                await _unitOfWork.CommitAsync();

                return Result<bool>.Ok(true);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error in RemoveQuntityAfterOrder for id: {id}");
                _backgroundJobClient.Enqueue(() =>
                    _errorNotificationService.SendErrorNotificationAsync(ex.Message, ex.StackTrace)
                );
                return Result<bool>.Fail("Error removing quantity", 500);
            }
        }
        #endregion

        #region Product Lifecycle Management Methods
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
        #endregion

        #region Create Operations Methods
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
                // Use mapper to create variant
                var variant = _mapper.MapToProductVariant(dto);
                variant.ProductId = productId;

                var result = await _unitOfWork.Repository<ProductVariant>().CreateAsync(variant);
                if (result == null)
                {
                    await transaction.RollbackAsync();
                    return Result<ProductVariantDto>.Fail("Failed to add variant", 400);
                }

                _logger.LogInformation($"Updating product {productId} quantity after adding variant");
                _backgroundJobClient.Enqueue(() => _productCatalogService.UpdateProductQuantity(productId));

                _logger.LogInformation($"Recording admin operation for adding variant to product {productId} by user {userId}");
                var isadded = await _adminOpreationServices.AddAdminOpreationAsync(
                    $"Add Variant to Product {productId}",
                    Opreations.AddOpreation,
                    userId,
                    productId
                );

                if (isadded == null)
                {
                    _logger.LogError($"Failed to record admin operation for adding variant to product {productId}");
                    return Result<ProductVariantDto>.Fail("Error adding variant", 500);
                }

                _logger.LogInformation($"Committing transaction for adding variant to product {productId}");
                await _unitOfWork.CommitAsync();
                await transaction.CommitAsync();
                _logger.LogInformation($"Successfully added variant to product {productId}");

                // Map to DTO and cache
                var variantDto = _mapper.MapToProductVariantDto(variant);
                await _cacheHelper.CacheVariantAsync(variant.Id, variantDto);
                _cacheHelper.RemoveProductCachesAsync();

                _logger.LogInformation($"Successfully completed adding variant (ID: {variant.Id}) to product {productId}");
                return Result<ProductVariantDto>.Ok(variantDto, "Variant added successfully", 201);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error in AddVariantAsync for productId: {productId}. Rolling back transaction.");
                await transaction.RollbackAsync();
                _logger.LogInformation($"Transaction rolled back for adding variant to product {productId}");
                _backgroundJobClient.Enqueue(() => _errorNotificationService.SendErrorNotificationAsync(ex.Message, ex.StackTrace));
                return Result<ProductVariantDto>.Fail("Error adding variant", 500);
            }
        }
        #endregion

        #region Update Operations Methods
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
                if (isexsist)
                {
                    _logger.LogWarning($"Attempt to update variant {id} with duplicate attributes for product {variant.ProductId}");
                    return Result<ProductVariantDto>.Fail("Thier's Varinat with this data ");
                }

                _logger.LogInformation($"Starting to update variant {id} properties");
                string updates = string.Empty;

                if (!string.IsNullOrEmpty(dto.Color) && dto.Color != variant.Color)
                {
                    _logger.LogInformation($"Updating variant {id} color from {variant.Color} to {dto.Color}");
                    updates += $"from {variant.Color} to {dto.Color}";
                    variant.Color = dto.Color;
                }
                if (dto.Size != null && dto.Size != variant.Size)
                {
                    _logger.LogInformation($"Updating variant {id} size from {variant.Size} to {dto.Size}");
                    updates += $"from {variant.Size} to {dto.Size}";
                    variant.Size = dto.Size;
                }
                if (dto.Waist.HasValue && dto.Waist != variant.Waist)
                {
                    _logger.LogInformation($"Updating variant {id} waist from {variant.Waist} to {dto.Waist}");
                    updates += $"from {variant.Waist} to {dto.Waist}";
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
                    $"Update Variant {id}" + updates,
                    Opreations.UpdateOpreation,
                    userId,
                    variant.ProductId
                );

                if (isadded == null)
                {
                    _logger.LogError($"Failed to record admin operation for updating variant {id}");
                    return Result<ProductVariantDto>.Fail("Error updating variant", 500);
                }

                _logger.LogInformation($"Committing transaction for updating variant {id}");
                await _unitOfWork.CommitAsync();
                await transaction.CommitAsync();
                _logger.LogInformation($"Successfully updated variant {id}");

                // Update cache and map to DTO
                var variantDto = _mapper.MapToProductVariantDto(variant);
                await _cacheHelper.CacheVariantAsync(id, variantDto);
               _cacheHelper.RemoveProductCachesAsync();

                return Result<ProductVariantDto>.Ok(variantDto, "Variant updated successfully", 200);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error in UpdateVariantAsync for id: {id}. Rolling back transaction.");
                await transaction.RollbackAsync();
                _logger.LogInformation($"Transaction rolled back for updating variant {id}");
                _backgroundJobClient.Enqueue(() => _errorNotificationService.SendErrorNotificationAsync(ex.Message, ex.StackTrace));
                return Result<ProductVariantDto>.Fail("Error updating variant", 500);
            }
        }
        #endregion

        #region Delete Operations Methods
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
                if (isinorders)
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

                if (isAdded == null)
                {
                    _logger.LogError($"Failed to record admin operation for deleting variant {id}");
                }

                await _unitOfWork.CommitAsync();
                await transaction.CommitAsync();

                // Update cache and check product status
               _cacheHelper.RemoveProductCachesAsync();
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
                _backgroundJobClient.Enqueue(() => _errorNotificationService.SendErrorNotificationAsync(ex.Message, ex.StackTrace));
                return Result<bool>.Fail("Error deleting variant", 500);
            }
        }
        #endregion


        public async Task<Result<bool>> ActivateVariantAsync(int id, string userId)
        {
            _logger.LogInformation($"Activating variant: {id}");
            using var transaction = await _unitOfWork.BeginTransactionAsync();
            try
            {
                _logger.LogInformation($"Checking if variant {id} exists and is not deleted");
                var varaintinfo = await _unitOfWork.ProductVariant.GetAll().Where(v => v.Id == id).Select(v => new
                {
                    hasquntity = v.Quantity > 0,
                    isdeleted = v.DeletedAt != null,
                    hassize = v.Size != null,
                    haslengthandwaist = v.Length != 0 && v.Waist != 0,
                    productid = v.ProductId
                }).FirstOrDefaultAsync();

                var variant = await _unitOfWork.ProductVariant.IsExsistAsync(id);
                if (varaintinfo == null || varaintinfo.isdeleted)
                {
                    _logger.LogWarning($"Variant {id} not found or is deleted, cannot activate");
                    return Result<bool>.Fail("Variant not found", 404);
                }

                _logger.LogInformation($"Validating variant {id} has required properties for activation");
                if (!(varaintinfo.hasquntity && (varaintinfo.haslengthandwaist || varaintinfo.hassize)))
                {
                    _logger.LogWarning($"Variant {id} does not meet activation requirements: hasQuantity={varaintinfo.hasquntity}, hasSize={varaintinfo.hassize}, hasLengthAndWaist={varaintinfo.haslengthandwaist}");
                    return Result<bool>.Fail("To activate, variant must have (length and waist) or size, and quantity > 0", 400);
                }

                _logger.LogInformation($"Attempting to activate variant {id}");
                var result = await _unitOfWork.ProductVariant.ActiveVaraintAsync(id);
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

                if (isAdded == null)
                {
                    _logger.LogError($"Failed to record admin operation for activating variant {id}");
                }

                _logger.LogInformation($"Committing unit of work for activating variant {id}");
                await _unitOfWork.CommitAsync();

                _logger.LogInformation($"Committing transaction for activating variant {id}");
                await transaction.CommitAsync();

                // Update cache
                _cacheHelper.RemoveProductCachesAsync();

                _logger.LogInformation($"Successfully completed all operations for activating variant {id}");
                return Result<bool>.Ok(true, "Variant activated", 200);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error in ActivateVariantAsync for id: {id}. Rolling back transaction.");
                await transaction.RollbackAsync();
                _logger.LogInformation($"Transaction rolled back for activating variant {id}");
                _backgroundJobClient.Enqueue(() => _errorNotificationService.SendErrorNotificationAsync(ex.Message, ex.StackTrace));
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

                if (varaintinfo == null || varaintinfo.isdeleted)
                    return Result<bool>.Fail("Variant not found", 404);

                _logger.LogInformation($"Attempting to deactivate variant {id}");
                var result = await _unitOfWork.ProductVariant.DeactiveVaraintAsync(id);
                if (!result)
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

                // Update cache and check product status
                _cacheHelper.RemoveProductCachesAsync();
                await CheckAndDeactivateProductIfAllVariantsInactiveOrZeroAsync(varaintinfo.productid);

                return Result<bool>.Ok(true, "Variant deactivated", 200);
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, $"Error in DeactivateVariantAsync for id: {id}");
                _backgroundJobClient.Enqueue(() => _errorNotificationService.SendErrorNotificationAsync(ex.Message, ex.StackTrace));
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

                 variant.Quantity += addQuantity;

      
                var adminopreation = await _adminOpreationServices.AddAdminOpreationAsync(
                    $"Add Quantity for Variant {id}",
                    Opreations.UpdateOpreation,
                    userId,
                    variant.ProductId
                );
                if(adminopreation==null)
                {
                    _logger.LogError($"Failed to record admin operation for adding quantity to variant {id}");
                    await transaction.RollbackAsync();
                  _backgroundJobClient.Enqueue(() => _errorNotificationService.SendErrorNotificationAsync("Failed to record admin operation for adding quantity", $"Variant ID: {id}, User ID: {userId}"));
					return Result<bool>.Fail("Error adding quantity", 500);
				}   


                await _unitOfWork.CommitAsync();
                await transaction.CommitAsync();

               _cacheHelper.RemoveProductCachesAsync();
				_backgroundJobClient.Enqueue(() => _productCatalogService.UpdateProductQuantity(variant.ProductId));

                return Result<bool>.Ok(true, "Quantity added successfully", 200);
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, $"Error in AddVariantQuantityAsync for id: {id}");
                _backgroundJobClient.Enqueue(() => _errorNotificationService.SendErrorNotificationAsync(ex.Message, ex.StackTrace));
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

                if (!isupdate.Success)
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

                if (isAdded == null)
                {
                    _logger.LogError($"Failed to record admin operation for removing quantity from variant {id}");
                    await transaction.RollbackAsync();
                    _backgroundJobClient.Enqueue(() => _errorNotificationService.SendErrorNotificationAsync("Failed to record admin operation for removing quantity", $"Variant ID: {id}, User ID: {userId}"));
					return Result<bool>.Fail("Error removing quantity", 500);


				}

                _logger.LogInformation($"Committing transaction for removing quantity from variant {id}");
                await _unitOfWork.CommitAsync();
                await transaction.CommitAsync();

				_cacheHelper.RemoveProductCachesAsync();

				return Result<bool>.Ok(true, "Quantity removed successfully", 200);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error in RemoveVariantQuantityAsync for id: {id}. Rolling back transaction.");
				await transaction.RollbackAsync();

				_logger.LogInformation($"Transaction rolled back for removing quantity from variant {id}");
                _backgroundJobClient.Enqueue(() => _errorNotificationService.SendErrorNotificationAsync(ex.Message, ex.StackTrace));
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
                var variantinfo = await _unitOfWork.ProductVariant.GetAll().Where(i => i.Id == id).Select(v => new
                {
                    productid = v.ProductId,
                    isdeleted = v.DeletedAt != null
                }).FirstOrDefaultAsync();

                if (variantinfo == null || !variantinfo.isdeleted)
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

                if (isAdded == null)
                {
                    _logger.LogError($"Failed to record admin operation for restoring variant {id}");
                }

                _logger.LogInformation($"Updating product {variantinfo.productid} quantity after restoring variant {id}");
                _productCatalogService.UpdateProductQuantity(variantinfo.productid);

                _logger.LogInformation($"Committing transaction for restoring variant {id}");
                await _unitOfWork.CommitAsync();


                _cacheHelper.RemoveProductCachesAsync();

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
                _backgroundJobClient.Enqueue(() => _errorNotificationService.SendErrorNotificationAsync(ex.Message, ex.StackTrace));
                return Result<bool>.Fail("Error restoring variant", 500);
            }
        }

    }
}
