using E_Commerce.DtoModels.InventoryDtos;
using E_Commerce.DtoModels.Responses;
using E_Commerce.DtoModels.WareHouseDtos;

namespace E_Commerce.Services.WareHouseServices
{
	public interface IWareHouseServices
	{
		public Task<Result<List<WareHouseDto>>> GetAllWareHousesAsync();
		public Task<Result<WareHouseDto>> GetWareHouseByIdAsync(int id);
		public Task<Result<WareHouseDto>> CreateWareHouseAsync(string userid, WareHouseDto wareHouse);
		public Task<Result<WareHouseDto>> UpdateWareHouseAsync(int id,string userid,WareHouseDto wareHouse);
		public Task<Result<string>> RemoveWareHouseAsync(int id,string userid);
		public Task<Result<InventoryDto>> AddInventoryToWareHouseAsync(int id,string userid,int Inventoryid);
		public Task<Result<WareHouseDto>> ReturnRemovedWareHouseAsync(int id,string userid);
		public Task<Result<string>> TransferProductsAsync(int from_warehouse_id, int to_warehouse_id, string userid,int Inventoryid);
		public Task<Result<string>> IsExsistAsync(int id);
	}
}
