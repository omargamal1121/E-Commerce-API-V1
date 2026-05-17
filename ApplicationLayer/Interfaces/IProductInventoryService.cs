using Application.DtoModels;
using Application.DtoModels.InventoryDtos;
using Application.DtoModels.Responses;
using Application.Services;

namespace Application.Interfaces
{
    public interface IProductInventoryService
    {
		Task<Result<InventoryDto>> CreateInventoryAsync(CreateInvetoryDto dto, string userId);
        Task<Result<InventoryDto>> TransferQuantityAsync(TransfereQuantityInvetoryDto dto, string userId);
        Task<Result<List<InventoryDto>>> GetWarehouseInventoryAsync(int warehouseId);
        Task<Result<InventoryDto>> GetInventoryById(int inventoryid);
        Task<Result<string>> DeleteInventoryAsync(int inventoryId, string userId);
        Task<Result<InventoryDto>> UpdateInventoryQuantityAsync(UpdateInventoryQuantityDto dto, string userId);

		Task<Result<List<InventoryDto>>> GetInventoryByProductIdAsync(int productId);
        Task<Result<List<InventoryDto>>> GetLowStockAlertsAsync(int threshold = 10);
        Task<Result<string>> BulkUpdateInventoryAsync(List<AddQuantityInvetoryDto> updates, string userId);
        Task<Result<List<InventoryDto>>> GetAllInventoryAsync(bool includeDeleted = false);
    }
} 

