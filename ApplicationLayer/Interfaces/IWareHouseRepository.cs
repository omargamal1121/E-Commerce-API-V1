using ApplicationLayer.Services;
using DomainLayer.Models;
using Newtonsoft.Json;

namespace ApplicationLayer.Interfaces
{
	public interface IWareHouseRepository:IRepository<Warehouse>
	{
		public  Task<Warehouse?> GetByNameAsync(string Name);

	}
}


