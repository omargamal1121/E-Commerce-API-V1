using AutoMapper;
using E_Commerce.DtoModels;
using E_Commerce.DtoModels.InventoryDtos;
using E_Commerce.DtoModels.ProductDtos;
using E_Commerce.DtoModels.Responses;
using E_Commerce.Enums;
using E_Commerce.ErrorHnadling;
using E_Commerce.Interfaces;
using E_Commerce.Models;
using E_Commerce.Services.AdminOperationServices;
using E_Commerce.Services.Cache;
using E_Commerce.UOW;
using Microsoft.EntityFrameworkCore;

namespace E_Commerce.Services.ProductInventoryServices
{
    public class ProductInventoryService : IProductInventoryService
    {
        private readonly ILogger<ProductInventoryService> _logger;
        private readonly IMapper _mapper;
        private readonly IUnitOfWork _unitOfWork;
        private readonly IAdminOpreationServices _adminOpreationServices;
        private readonly ICacheManager _cacheManager;
        private const string CACHE_TAG_PRODUCT = "product";
        private const string CACHE_TAG_INVENTORY = "inventory";

        public ProductInventoryService(
            ILogger<ProductInventoryService> logger,
            IMapper mapper,
            IUnitOfWork unitOfWork,
            IAdminOpreationServices adminOpreationServices,
            ICacheManager cacheManager)
        {
            _logger = logger;
            _mapper = mapper;
            _unitOfWork = unitOfWork;
            _adminOpreationServices = adminOpreationServices;
            _cacheManager = cacheManager;
        }

        public async Task<Result<InventoryDto>> CreateInventoryAsync(CreateInvetoryDto dto, string userId)
        {
            _logger.LogInformation($"Executing {nameof(CreateInventoryAsync)}");
            var product = await _unitOfWork.Product.GetByIdAsync(dto.ProductId);
            if (product == null)
            {
                return Result<InventoryDto>.Fail("Product not found", 404);
            }
            var warehouse = await _unitOfWork.WareHouse.GetByIdAsync(dto.WareHouseId);
            if (warehouse == null)
            {
                return Result<InventoryDto>.Fail("Warehouse not found", 404);
            }
            var existingInventory = await _unitOfWork.Repository<ProductInventory>()
                .GetByQuery(i => i.ProductId == dto.ProductId && i.WarehouseId == dto.WareHouseId);
            if (existingInventory != null)
            {
                return Result<InventoryDto>.Fail("Product already exists in this warehouse", 409);
            }
            using var transaction = await _unitOfWork.BeginTransactionAsync();
            try
            {
                var inventory = new ProductInventory
                {
                    ProductId = dto.ProductId,
                    WarehouseId = dto.WareHouseId,
                    Quantity = dto.Quantity,
                    CreatedAt = DateTime.UtcNow,
                };
                var result = await _unitOfWork.Repository<ProductInventory>().CreateAsync(inventory);
                if (result == null)
                {
                    await transaction.RollbackAsync();
                    return Result<InventoryDto>.Fail("Failed to create inventory entry", 500);
                }
                var adminLog = await _adminOpreationServices.AddAdminOpreationAsync(
                    $"Created inventory for product {dto.ProductId} in warehouse {dto.WareHouseId} with quantity {dto.Quantity}",
                    Opreations.AddOpreation,
                    userId,
                    dto.ProductId
                );
                if (adminLog == null)
                {
                    await transaction.RollbackAsync();
                    return Result<InventoryDto>.Fail("Failed to log operation", 500);
                }
                await _unitOfWork.CommitAsync();
                await transaction.CommitAsync();
                await _cacheManager.RemoveByTagAsync(CACHE_TAG_INVENTORY);
                await _cacheManager.RemoveByTagAsync(CACHE_TAG_PRODUCT);
                var inventoryDto = _mapper.Map<InventoryDto>(inventory);
                return Result<InventoryDto>.Ok(inventoryDto, "Inventory created successfully", 201);
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError($"Error in {nameof(CreateInventoryAsync)}: {ex.Message}");
                return Result<InventoryDto>.Fail("An error occurred while processing your request", 500);
            }
        }

        public async Task<Result<InventoryDto>> UpdateInventoryQuantityAsync(UpdateInventoryQuantityDto dto, string userId)
        {
            _logger.LogInformation($"Executing {nameof(UpdateInventoryQuantityAsync)}");
            var inventory = await _unitOfWork.Repository<ProductInventory>()
                .GetByQuery(i => i.ProductId == dto.ProductId && i.WarehouseId == dto.WarehouseId);
            if (inventory == null)
            {
                return Result<InventoryDto>.Fail("Inventory not found", 404);
            }
            var product = await _unitOfWork.Product.GetByIdAsync(dto.ProductId);
            if (product == null)
            {
                return Result<InventoryDto>.Fail("Product not found", 404);
            }
            using var transaction = await _unitOfWork.BeginTransactionAsync();
            try
            {
                inventory.Quantity = dto.NewQuantity;
                inventory.ModifiedAt = DateTime.UtcNow;
                var updateResult =  _unitOfWork.Repository<ProductInventory>().Update(inventory);
                if (updateResult == null)
                {
                    await transaction.RollbackAsync();
                    return Result<InventoryDto>.Fail("Failed to update inventory quantity", 500);
                }
                var adminLog = await _adminOpreationServices.AddAdminOpreationAsync(
                    $"Updated inventory quantity for product {dto.ProductId} in warehouse {dto.WarehouseId} to {dto.NewQuantity}",
                    Opreations.UpdateOpreation,
                    userId,
                    dto.ProductId
                );
                if (adminLog == null)
                {
                    await transaction.RollbackAsync();
                    return Result<InventoryDto>.Fail("Failed to log operation", 500);
                }
                await _unitOfWork.CommitAsync();
                await transaction.CommitAsync();
                await _cacheManager.RemoveByTagAsync(CACHE_TAG_INVENTORY);
                await _cacheManager.RemoveByTagAsync(CACHE_TAG_PRODUCT);
                var inventoryDto = _mapper.Map<InventoryDto>(inventory);
                return Result<InventoryDto>.Ok(inventoryDto, "Inventory quantity updated successfully", 200);
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError($"Error in {nameof(UpdateInventoryQuantityAsync)}: {ex.Message}");
                return Result<InventoryDto>.Fail("An error occurred while processing your request", 500);
            }
        }

        public async Task<Result<InventoryDto>> TransferQuantityAsync(TransfereQuantityInvetoryDto dto, string userId)
        {
            _logger.LogInformation($"Executing {nameof(TransferQuantityAsync)}");
            var sourceInventory = await _unitOfWork.Repository<ProductInventory>()
                .GetByIdAsync(dto.FromInventoryId);
            if (sourceInventory == null)
            {
                return Result<InventoryDto>.Fail("Source inventory not found", 404);
            }
            var targetInventory = await _unitOfWork.Repository<ProductInventory>()
                .GetByIdAsync(dto.ToInventoryId);
            if (targetInventory == null)
            {
                return Result<InventoryDto>.Fail("Target inventory not found", 404);
            }
            if (sourceInventory.ProductId != dto.ProductId || targetInventory.ProductId != dto.ProductId)
            {
                return Result<InventoryDto>.Fail("Product mismatch between inventories", 400);
            }
            if (sourceInventory.Quantity < dto.Quantity)
            {
                return Result<InventoryDto>.Fail("Insufficient quantity in source inventory", 400);
            }
            using var transaction = await _unitOfWork.BeginTransactionAsync();
            try
            {
                sourceInventory.Quantity -= dto.Quantity;
                targetInventory.Quantity += dto.Quantity;
                var sourceUpdate =  _unitOfWork.Repository<ProductInventory>().Update(sourceInventory);
                var targetUpdate =  _unitOfWork.Repository<ProductInventory>().Update(targetInventory);
                if (sourceUpdate == null || targetUpdate == null)
                {
                    await transaction.RollbackAsync();
                    return Result<InventoryDto>.Fail("Failed to update inventory quantities", 500);
                }
                var adminLog = await _adminOpreationServices.AddAdminOpreationAsync(
                    $"Transferred {dto.Quantity} units of product {dto.ProductId} from inventory {dto.FromInventoryId} to {dto.ToInventoryId}",
                    Opreations.UpdateOpreation,
                    userId,
                    dto.ProductId
                );
                if (adminLog == null)
                {
                    await transaction.RollbackAsync();
                    return Result<InventoryDto>.Fail("Failed to log operation", 500);
                }
                await _unitOfWork.CommitAsync();
                await transaction.CommitAsync();
                await _cacheManager.RemoveByTagAsync(CACHE_TAG_INVENTORY);
                var inventoryDto = _mapper.Map<InventoryDto>(targetInventory);
                return Result<InventoryDto>.Ok(inventoryDto, "Transfer completed successfully", 200);
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError($"Error in {nameof(TransferQuantityAsync)}: {ex.Message}");
                return Result<InventoryDto>.Fail("An error occurred while processing your request", 500);
            }
        }

        public async Task<Result<List<InventoryDto>>> GetWarehouseInventoryAsync(int warehouseId)
        {
            _logger.LogInformation($"Executing {nameof(GetWarehouseInventoryAsync)}");
            var cacheKey = $"{CACHE_TAG_INVENTORY}warehouse:{warehouseId}";
            var cachedInventory = await _cacheManager.GetAsync<List<InventoryDto>>(cacheKey);
            if (cachedInventory != null)
            {
                return Result<List<InventoryDto>>.Ok(cachedInventory, "Inventory retrieved from cache", 200);
            }
            var warehouse = await _unitOfWork.WareHouse.GetByIdAsync(warehouseId);
            if (warehouse == null)
            {
                return Result<List<InventoryDto>>.Fail("Warehouse not found", 404);
            }
            var inventory = warehouse.ProductInventories
                .Where(i => i.DeletedAt == null)
                .Select(i => _mapper.Map<InventoryDto>(i))
                .ToList();
            await _cacheManager.SetAsync(cacheKey, inventory, tags: new[] { CACHE_TAG_INVENTORY });
            return Result<List<InventoryDto>>.Ok(inventory, "Inventory retrieved successfully", 200);
        }

        public async Task<Result<string>> DeleteInventoryAsync(int inventoryId, string userId)
        {
            _logger.LogInformation($"Executing {nameof(DeleteInventoryAsync)}");
            var inventory = await _unitOfWork.Repository<ProductInventory>().GetByIdAsync(inventoryId);
            if (inventory == null)
            {
                return Result<string>.Fail("Inventory not found", 404);
            }
            if (inventory.Quantity > 0)
            {
                return Result<string>.Fail("Cannot delete inventory with remaining quantity", 400);
            }
            using var transaction = await _unitOfWork.BeginTransactionAsync();
            try
            {
                inventory.DeletedAt = DateTime.UtcNow;
                var updateResult =   _unitOfWork.Repository<ProductInventory>().Update(inventory);
                if (updateResult == null)
                {
                    await transaction.RollbackAsync();
                    return Result<string>.Fail("Failed to delete inventory", 500);
                }
                var adminLog = await _adminOpreationServices.AddAdminOpreationAsync(
                    $"Deleted inventory {inventoryId}",
                    Opreations.DeleteOpreation,
                    userId,
                    inventory.ProductId
                );
                if (adminLog == null)
                {
                    await transaction.RollbackAsync();
                    return Result<string>.Fail("Failed to log operation", 500);
                }
                await _unitOfWork.CommitAsync();
                await transaction.CommitAsync();
                await _cacheManager.RemoveByTagAsync(CACHE_TAG_INVENTORY);
                return Result<string>.Ok("Inventory deleted successfully", "Inventory deleted successfully", 200);
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError($"Error in {nameof(DeleteInventoryAsync)}: {ex.Message}");
                return Result<string>.Fail("An error occurred while processing your request", 500);
            }
        }

        public async Task<Result<List<InventoryDto>>> GetInventoryByProductIdAsync(int productId)
        {
            _logger.LogInformation($"Executing {nameof(GetInventoryByProductIdAsync)}");
            var cacheKey = $"{CACHE_TAG_INVENTORY}product:{productId}";
            var cachedInventory = await _cacheManager.GetAsync<List<InventoryDto>>(cacheKey);
            if (cachedInventory != null)
            {
                return Result<List<InventoryDto>>.Ok(cachedInventory, "Inventory retrieved from cache", 200);
            }
            var product = await _unitOfWork.Product.GetByIdAsync(productId);
            if (product == null)
            {
                _logger.LogWarning($"Product not found with id: {productId}");
                return Result<List<InventoryDto>>.Fail("Product not found", 404);
            }
            var inventory = product.InventoryEntries
                .Where(i => i.DeletedAt == null)
                .Select(i => _mapper.Map<InventoryDto>(i))
                .ToList();
            await _cacheManager.SetAsync(cacheKey, inventory, tags: new[] { CACHE_TAG_INVENTORY });
            return Result<List<InventoryDto>>.Ok(inventory, "Inventory retrieved successfully", 200);
        }

        public async Task<Result<List<InventoryDto>>> GetLowStockAlertsAsync(int threshold = 10)
        {
            _logger.LogInformation($"Executing {nameof(GetLowStockAlertsAsync)} with threshold: {threshold}");
            var cacheKey = $"{CACHE_TAG_INVENTORY}lowstock:{threshold}";
            var cachedAlerts = await _cacheManager.GetAsync<List<InventoryDto>>(cacheKey);
            if (cachedAlerts != null)
            {
                return Result<List<InventoryDto>>.Ok(cachedAlerts, "Low stock alerts retrieved from cache", 200);
            }
            var lowStockInventory =  _unitOfWork.Repository<ProductInventory>()
                .GetAll();
            if (lowStockInventory == null)
            {
                return Result<List<InventoryDto>>.Fail("Failed to retrieve low stock inventory", 500);
            }
            var alerts = lowStockInventory
                .Select(i => _mapper.Map<InventoryDto>(i))
                .ToList();
            await _cacheManager.SetAsync(cacheKey, alerts, tags: new[] { CACHE_TAG_INVENTORY });
            return Result<List<InventoryDto>>.Ok(alerts, $"Found {alerts.Count} items with low stock", 200);
        }

        public async Task<Result<string>> BulkUpdateInventoryAsync(List<AddQuantityInvetoryDto> updates, string userId)
        {
            _logger.LogInformation($"Executing {nameof(BulkUpdateInventoryAsync)} with {updates.Count} updates");
            using var transaction = await _unitOfWork.BeginTransactionAsync();
            try
            {
                foreach (var update in updates)
                {
                    var inventory = await _unitOfWork.Repository<ProductInventory>().GetByIdAsync(update.Id);
                    if (inventory == null)
                    {
                        await transaction.RollbackAsync();
                        return Result<string>.Fail($"Inventory not found for ID: {update.Id}", 404);
                    }
                    inventory.Quantity = update.Quantity;
                    inventory.ModifiedAt = DateTime.UtcNow;
                    var updateResult =  _unitOfWork.Repository<ProductInventory>().Update(inventory);
                    if (updateResult == null)
                    {
                        await transaction.RollbackAsync();
                        return Result<string>.Fail($"Failed to update inventory {update.Id}", 500);
                    }
                }
                var adminLog = await _adminOpreationServices.AddAdminOpreationAsync(
                    $"Bulk updated {updates.Count} inventory items",
                    Opreations.UpdateOpreation,
                    userId,
                    0
                );
                if (adminLog == null)
                {
                    await transaction.RollbackAsync();
                    return Result<string>.Fail("Failed to log operation", 500);
                }
                await _unitOfWork.CommitAsync();
                await transaction.CommitAsync();
                await _cacheManager.RemoveByTagAsync(CACHE_TAG_INVENTORY);
                return Result<string>.Ok($"Successfully updated {updates.Count} inventory items", $"Successfully updated {updates.Count} inventory items", 200);
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError($"Error in {nameof(BulkUpdateInventoryAsync)}: {ex.Message}");
                return Result<string>.Fail("An error occurred while performing bulk update", 500);
            }
        }

        public async Task<Result<List<InventoryDto>>> GetAllInventoryAsync(bool includeDeleted = false)
        {
            _logger.LogInformation($"Executing {nameof(GetAllInventoryAsync)} with includeDeleted: {includeDeleted}");
            var cacheKey = $"{CACHE_TAG_INVENTORY}all:{includeDeleted}";
            var cachedInventory = await _cacheManager.GetAsync<List<InventoryDto>>(cacheKey);
            if (cachedInventory != null)
            {
                return Result<List<InventoryDto>>.Ok(cachedInventory, "Inventory retrieved from cache", 200);
            }
            var inventoryQuery =    _unitOfWork.Repository<ProductInventory>().GetAll();
            if(inventoryQuery == null)
            {
                return Result<List<InventoryDto>>.Fail("Try Again later", 500);
            }
            if (!includeDeleted)
            {
                inventoryQuery = inventoryQuery.Where(i => i.DeletedAt == null);
            }
            var inventory = await inventoryQuery
                .Select(i => _mapper.Map<InventoryDto>(i))
                .ToListAsync();
            await _cacheManager.SetAsync(cacheKey, inventory, tags: new[] { CACHE_TAG_INVENTORY });
            return Result<List<InventoryDto>>.Ok(inventory, $"Retrieved {inventory.Count} inventory items", 200);
        }

        public async Task<Result<InventoryDto>> GetInventoryById(int id)
        {
            _logger.LogInformation($"Executing {nameof(GetInventoryById)} for ID: {id}");
            var cacheKey = $"{CACHE_TAG_INVENTORY}id:{id}";
            var cachedInventory = await _cacheManager.GetAsync<InventoryDto>(cacheKey);
            if (cachedInventory != null)
            {
                return Result<InventoryDto>.Ok(cachedInventory, "Inventory retrieved from cache", 200);
            }
            var inventory = await _unitOfWork.ProductInventory.GetByInvetoryIdWithProductAsync(id);
            if (inventory == null)
            {
                return Result<InventoryDto>.Fail("Inventory not found", 404);
            }
            var inventoryDto = _mapper.Map<InventoryDto>(inventory);
            await _cacheManager.SetAsync(cacheKey, inventoryDto, tags: new[] { CACHE_TAG_INVENTORY });
            return Result<InventoryDto>.Ok(inventoryDto, "Inventory retrieved successfully", 200);
        }
    }
} 