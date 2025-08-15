using E_Commerce.Services;
using E_Commerce.Models;
using Newtonsoft.Json;

namespace E_Commerce.Interfaces
{
	public interface IWareHouseRepository:IRepository<Warehouse>
	{
		public  Task<Warehouse?> GetByNameAsync(string Name);

	}
}
