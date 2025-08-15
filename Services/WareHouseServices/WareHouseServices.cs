using AutoMapper;
using E_Commerce.DtoModels.InventoryDtos;
using E_Commerce.DtoModels.Responses;
using E_Commerce.DtoModels.WareHouseDtos;
using E_Commerce.Enums;
using E_Commerce.ErrorHnadling;
using E_Commerce.Interfaces;
using E_Commerce.Models;
using E_Commerce.Services.AdminOpreationServices;
using E_Commerce.Services.Cache;
using E_Commerce.UOW;
using Microsoft.EntityFrameworkCore;

namespace E_Commerce.Services.WareHouseServices
{
	public class WareHouseServices : IWareHouseServices
	{
		private ILogger<WareHouseServices> _logger;
		private IUnitOfWork _unitOfWork;
		private IMapper _mapper;
		private IAdminOpreationServices _adminOpreationServices ;
		private ICacheManager _cacheManager ;
		private const string CACH_TAGE = "WareHouse";


		public WareHouseServices(ICacheManager cacheManager,IAdminOpreationServices adminOpreationServices,IMapper mapper,ILogger<WareHouseServices> logger, IUnitOfWork unitOfWork)
		{
			_cacheManager = cacheManager;
			_adminOpreationServices= adminOpreationServices;
			_mapper = mapper;
			_logger = logger;
			_unitOfWork = unitOfWork;
		}
		public Task<ApiResponse<InventoryDto>> AddInventoryToWareHouseAsync(int id, string userid, int Inventoryid)
		{
			throw new NotImplementedException();
		}

		public async Task<Result<WareHouseDto>> CreateWareHouseAsync(string userid, WareHouseDto wareHouse)
		{
			_logger.LogInformation($"Execute:{nameof(CreateWareHouseAsync)}");
			using var transction=await _unitOfWork.BeginTransactionAsync();
			var warehouse=_mapper.Map<Warehouse>(wareHouse);
			if(warehouse == null)
			{
				_logger.LogError("Mapping Filed");
				return Result<WareHouseDto>.Fail("Try Again later", 500);
			}
			var nameCheck = await _unitOfWork.WareHouse.GetByNameAsync(wareHouse.Name);
			if (nameCheck != null)
			{
				_logger.LogWarning($"Warehouse name {wareHouse.Name} is already in use");
				return Result<WareHouseDto>.Fail("Warehouse name is already in use", 409);
			}
			var iscreated=await _unitOfWork.WareHouse.CreateAsync(warehouse);
			if (iscreated == null) 
			{
				_logger.LogError("Error While Createing Warehouse");
				await transction.RollbackAsync();
				return Result<WareHouseDto>.Fail("Try Again later", 500);
			}
			_logger.LogInformation("Created ");
			await _cacheManager.RemoveByTagAsync(CACH_TAGE);
			await _unitOfWork.CommitAsync();
			var isadded = await _adminOpreationServices.AddAdminOpreationAsync("Add WareHouse", Opreations.AddOpreation, userid, iscreated.Id);
			if(isadded == null)
			{
				_logger.LogError("Failed to log admin operation");
				await transction.RollbackAsync();
				return Result<WareHouseDto>.Fail("Try Again later", 500);
			}
			_logger.LogInformation($"Admin Opreation added with id:{isadded.Data.Id}");
			await _unitOfWork.CommitAsync();
			await transction.CommitAsync();
			var createdwarehouse = _mapper.Map<WareHouseDto>(iscreated);
			return Result<WareHouseDto>.Ok(createdwarehouse, "Created", 201);
		}

		public async Task<Result<List<WareHouseDto>>> GetAllWareHousesAsync()
		{
			_logger.LogInformation($"Execute:{nameof(GetAllWareHousesAsync)}");
			string cach_key = "GetAllWareHouse";
			var cached_data= await _cacheManager.GetAsync<List<WareHouseDto>>(cach_key);
			if(cached_data != null)
			{
				_logger.LogInformation("From Cach");
				return Result<List<WareHouseDto>>.Ok(cached_data, "Get WareHouse", 200);
			}
			var warehousereult= _unitOfWork.WareHouse.GetAll();
			if(warehousereult == null)
			{
				_logger.LogError("Error fetching warehouses");
				return Result<List<WareHouseDto>>.Fail("Try Again later", 500);
			}
			var warehousesdto= await warehousereult.Where(w=>w.DeletedAt==null).Select(w=>_mapper.Map<WareHouseDto>(w)).ToListAsync();
			if (warehousesdto is null)
			{
				_logger.LogError("Can't Mapping");
				return Result<List<WareHouseDto>>.Fail("Try Again later", 500);
			}
			await _cacheManager.SetAsync(cach_key,warehousesdto,tags:new string[] { CACH_TAGE });
			return Result<List<WareHouseDto>>.Ok(warehousesdto, "Get WareHouse", 200);
		}

		public async Task<Result<WareHouseDto>> GetWareHouseByIdAsync(int id)
		{
			_logger.LogInformation($"Execute:{nameof(GetWareHouseByIdAsync)} with  id:{id}");
			var warehouse= await _unitOfWork.WareHouse.GetByIdAsync(id);
			if (warehouse == null)
			{
				_logger.LogWarning($"Can't found warehouse with this id {id}");
				return Result<WareHouseDto>.Fail($"Can't found warehouse with this id {id}", 404);
			}
			string key = $"WareHouse with id:{id}";
			var warehousedto = _mapper.Map<WareHouseDto>(warehouse);
			if(warehousedto is null)
			{
				_logger.LogError("Can't mapping");
				return Result<WareHouseDto>.Fail("Try Again later", 500);
			}
			await _cacheManager.SetAsync(key, warehousedto,tags: new string []{CACH_TAGE});
			_logger.LogInformation("WareHouse found");
			return Result<WareHouseDto>.Ok(warehousedto, "Warehouse Found", 200);
		}

		public async Task<Result<string>> RemoveWareHouseAsync(int id, string userid)
		{
			_logger.LogInformation($"Execute:{nameof(RemoveWareHouseAsync)}");
			using var transaction = await _unitOfWork.BeginTransactionAsync();
			var warehouse = await _unitOfWork.WareHouse.GetByIdAsync(id);
			if (warehouse == null)
			{
				_logger.LogError($"Can't found warehouse with this id {id}");
				await transaction.RollbackAsync();
				return Result<string>.Fail($"Can't found warehouse with this id {id}", 404);
			}
			// Check if warehouse has any products
			if (warehouse.ProductInventories.Count > 0)
			{
				_logger.LogError("Can't delete warehouse that contains products");
				await transaction.RollbackAsync();
				return Result<string>.Fail("Cannot delete warehouse that contains products", 400);
			}
			warehouse.DeletedAt = DateTime.UtcNow;
			var isRemoved = _unitOfWork.WareHouse.Update(warehouse);
			if (!isRemoved)
			{
				_logger.LogError("Failed to update warehouse");
				await transaction.RollbackAsync();
				return Result<string>.Fail("Failed to update warehouse", 500);
			}
			await _cacheManager.RemoveByTagAsync(CACH_TAGE);
			await _unitOfWork.CommitAsync();
			var isAdded = await _adminOpreationServices.AddAdminOpreationAsync("soft delete to warehouse ", Opreations.DeleteOpreation, userid, id);
			if (isAdded == null)
			{
				_logger.LogError("Failed to log admin operation");
				await transaction.RollbackAsync();
				return Result<string>.Fail("Failed to log operation", 500);
			}
			_logger.LogInformation($"Admin Operation added with id:{isAdded.Data.Id}");
			await _unitOfWork.CommitAsync();
			await transaction.CommitAsync();
			return Result<string>.Ok("Warehouse removed", "Warehouse removed successfully", 200);
		}

		public async Task<Result<string>> TransferProductsAsync(int from_warehouse_id, int to_warehouse_id, string userid, int Inventoryid)
		{
			_logger.LogInformation($"Execute:{nameof(TransferProductsAsync)}");
			using var transaction = await _unitOfWork.BeginTransactionAsync();
			var sourceWarehouse = await _unitOfWork.WareHouse.GetByIdAsync(from_warehouse_id);
			if (sourceWarehouse == null)
			{
				_logger.LogWarning($"Source warehouse not found with id: {from_warehouse_id}");
				return Result<string>.Fail($"Warehouse with id {from_warehouse_id} not found", 404);
			}
			var targetWarehouse = await _unitOfWork.WareHouse.GetByIdAsync(to_warehouse_id);
			if (targetWarehouse == null)
			{
				_logger.LogWarning($"Target warehouse not found with id: {to_warehouse_id}");
				return Result<string>.Fail($"Warehouse with id {to_warehouse_id} not found", 404);
			}
			var sourceInventory = sourceWarehouse.ProductInventories.FirstOrDefault(pi => pi.Id == Inventoryid);
			if (sourceInventory == null)
			{
				_logger.LogWarning($"Inventory {Inventoryid} not found in source warehouse {from_warehouse_id}");
				return Result<string>.Fail($"Inventory {Inventoryid} not found in source warehouse", 404);
			}
			try
			{
				var existingInventory = targetWarehouse.ProductInventories.FirstOrDefault(pi => pi.ProductId == sourceInventory.ProductId);
				if (existingInventory != null)
				{
					existingInventory.Quantity += sourceInventory.Quantity;
					existingInventory.ModifiedAt = DateTime.UtcNow;
					var updateResult = _unitOfWork.Repository<ProductInventory>().Update(existingInventory);
					if (!updateResult)
					{
						_logger.LogError("Failed to update inventory in target warehouse");
						await transaction.RollbackAsync();
						return Result<string>.Fail("Failed to update target warehouse inventory", 500);
					}
				}
				else
				{
					var newInventory = new ProductInventory
					{
						ProductId = sourceInventory.ProductId,
						WarehouseId = to_warehouse_id,
						Quantity = sourceInventory.Quantity,
						CreatedAt = DateTime.UtcNow,
						ModifiedAt = DateTime.UtcNow
					};
					var createResult = await _unitOfWork.Repository<ProductInventory>().CreateAsync(newInventory);
					if (createResult == null)
					{
						_logger.LogError("Failed to create inventory in target warehouse");
						await transaction.RollbackAsync();
						return Result<string>.Fail("Failed to create inventory in target warehouse", 500);
					}
				}
				sourceInventory.DeletedAt = DateTime.UtcNow;
				var removeResult = _unitOfWork.Repository<ProductInventory>().Update(sourceInventory);
				if (!removeResult)
				{
					_logger.LogError("Failed to remove inventory from source warehouse");
					await transaction.RollbackAsync();
					return Result<string>.Fail("Failed to remove inventory from source warehouse", 500);
				}
				var adminLog = await _adminOpreationServices.AddAdminOpreationAsync(
					$"Transferred inventory {Inventoryid} from warehouse {from_warehouse_id} to warehouse {to_warehouse_id}",
					Opreations.UpdateOpreation,
					userid,
					Inventoryid
				);
				if (adminLog == null)
				{
					_logger.LogError("Failed to log admin operation");
					await transaction.RollbackAsync();
					return Result<string>.Fail("Failed to log operation", 500);
				}
				await _cacheManager.RemoveByTagAsync(CACH_TAGE);
				await _unitOfWork.CommitAsync();
				await transaction.CommitAsync();
				return Result<string>.Ok($"Transferred {sourceInventory.Quantity} units of product {sourceInventory.ProductId} from warehouse {from_warehouse_id} to warehouse {to_warehouse_id}", "Products transferred successfully", 200);
			}
			catch (Exception ex)
			{
				_logger.LogError($"Error in {nameof(TransferProductsAsync)}: {ex.Message}");
				await transaction.RollbackAsync();
				return Result<string>.Fail("An error occurred while transferring products", 500);
			}
		}

		public async Task<Result<WareHouseDto>> UpdateWareHouseAsync(int id, string userid, WareHouseDto wareHouse)
		{
			_logger.LogInformation($"Execute:{nameof(UpdateWareHouseAsync)}");
			using var transaction = await _unitOfWork.BeginTransactionAsync();
			var existingWarehouse = await _unitOfWork.WareHouse.GetByIdAsync(id);
			if (existingWarehouse == null)
			{
				_logger.LogWarning($"Warehouse not found with id: {id}");
				return Result<WareHouseDto>.Fail($"Warehouse with id {id} not found", 404);
			}
			try
			{
				if (!string.IsNullOrEmpty(wareHouse.Name))
				{
					if (existingWarehouse.Name.Equals(wareHouse.Name, StringComparison.OrdinalIgnoreCase))
					{
						_logger.LogWarning($"Same Name ID: {id}");
						return Result<WareHouseDto>.Fail("Can't Use Same Name", 409);
					}
					var nameCheck = await _unitOfWork.WareHouse.GetByNameAsync(wareHouse.Name);
					if (nameCheck != null)
					{
						_logger.LogWarning($"Warehouse name {wareHouse.Name} is already in use");
						return Result<WareHouseDto>.Fail("Warehouse name is already in use", 400);
					}
					existingWarehouse.Name = wareHouse.Name;
				}
				if (!string.IsNullOrEmpty(wareHouse.Address))
				{
					existingWarehouse.Address = wareHouse.Address;
				}
				if (!string.IsNullOrEmpty(wareHouse.Phone))
				{
					existingWarehouse.Phone = wareHouse.Phone;
				}
				existingWarehouse.ModifiedAt = DateTime.UtcNow;
				var updateResult =	_unitOfWork.WareHouse.Update(existingWarehouse);
				if (!updateResult)
				{
					_logger.LogError("Failed to update warehouse");
					await transaction.RollbackAsync();
					return Result<WareHouseDto>.Fail("Failed to update warehouse", 500);
				}
				var adminLog = await _adminOpreationServices.AddAdminOpreationAsync(
					$"Updated warehouse {id}",
					Opreations.UpdateOpreation,
					userid,
					id
				);
				if (adminLog == null)
				{
					_logger.LogError("Failed to log admin operation");
					await transaction.RollbackAsync();
					return Result<WareHouseDto>.Fail("Failed to log operation", 500);
				}
				await _cacheManager.RemoveByTagAsync(CACH_TAGE);
				await _unitOfWork.CommitAsync();
				await transaction.CommitAsync();
				var updatedWarehouseDto = _mapper.Map<WareHouseDto>(existingWarehouse);
				if (updatedWarehouseDto == null)
				{
					_logger.LogError("Failed to map updated warehouse to DTO");
					return Result<WareHouseDto>.Fail("Failed to map warehouse data", 500);
				}
				return Result<WareHouseDto>.Ok(updatedWarehouseDto, "Warehouse updated successfully", 200);
			}
			catch (Exception ex)
			{
				_logger.LogError($"Error in {nameof(UpdateWareHouseAsync)}: {ex.Message}");
				await transaction.RollbackAsync();
				return Result<WareHouseDto>.Fail("An error occurred while updating warehouse", 500);
			}
		}

		public async Task<Result<string>> IsExsistAsync(int id)
		{
			var isexsist= await _unitOfWork.WareHouse.IsExsistAsync(id);
			return isexsist ? Result<string>.Ok("Found", "Found", 200) : Result<string>.Fail($"No Warehouse with this id:{id}", 404);
		}

		public async Task<Result<WareHouseDto>> ReturnRemovedWareHouseAsync(int id, string userid)
		{
			_logger.LogInformation($"Execute:{nameof(ReturnRemovedWareHouseAsync)}");
			var isdeleted= await _unitOfWork.WareHouse.IsDeletedAsync(id);
			if (!isdeleted) 
			{
				_logger.LogWarning($"No WareHouse Deleted with this id:{id}");
				return Result<WareHouseDto>.Fail($"No WareHouse Deleted with this id:{id}", 404);
			}
			var deletdwarehouse=await _unitOfWork.WareHouse.GetByIdAsync(id);
			if(deletdwarehouse == null)
			{
				return Result<WareHouseDto>.Fail($"No WareHouse Deleted with this id:{id}", 404);
			}
			using var transaction= await _unitOfWork.BeginTransactionAsync();
			try
			{
				deletdwarehouse.DeletedAt = null;
				var isupdated = _unitOfWork.WareHouse.Update(deletdwarehouse);
				if (!isupdated)
				{
					_logger.LogError("Failed to update warehouse");
					await transaction.RollbackAsync();
					return Result<WareHouseDto>.Fail("Try Again Later", 500);
				}
				var isadded = await _adminOpreationServices.AddAdminOpreationAsync("Return WareHouse From Deleted", Opreations.UndoDeleteOpreation, userid, id);
				if(isadded == null)
				{
					_logger.LogError("Failed to log admin operation");
					await transaction.RollbackAsync();
					return Result<WareHouseDto>.Fail("Try Again Later", 500);
				}
				await _unitOfWork.CommitAsync();
				await transaction.CommitAsync();
				var warehousedto = _mapper.Map<WareHouseDto>(deletdwarehouse);
				if(warehousedto==null)
				{
					return Result<WareHouseDto>.Fail("Try Again Later", 500);
				}
				return Result<WareHouseDto>.Ok(warehousedto, "Done", 200);
			}
			catch (Exception ex)
			{
				_logger.LogError(ex.Message);
				await transaction.RollbackAsync();
				return Result<WareHouseDto>.Fail("Try Again Later", 500);
			}
		}

		Task<Result<InventoryDto>> IWareHouseServices.AddInventoryToWareHouseAsync(int id, string userid, int Inventoryid)
		{
			throw new NotImplementedException();
		}
	}
}
