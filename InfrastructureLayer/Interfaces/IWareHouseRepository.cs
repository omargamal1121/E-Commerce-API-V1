
using Domain.Models;
using Newtonsoft.Json;

namespace Infrastructure.Interfaces
{
	public interface IWareHouseRepository:IRepository<Warehouse>
	{
		public  Task<Warehouse?> GetByNameAsync(string Name);

	}
}


