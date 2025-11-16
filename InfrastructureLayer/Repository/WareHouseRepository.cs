using InfrastructureLayer.Context;

using DomainLayer.Models;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using StackExchange.Redis;
using ApplicationLayer.Interfaces;
using Microsoft.Extensions.Logging;
namespace InfrastructureLayer.Repository
{
	public class WareHouseRepository:MainRepository<Warehouse> ,IWareHouseRepository
	{
		private readonly AppDbContext _context;
		private readonly DbSet<Warehouse> _warehouses;
		private readonly ILogger<MainRepository<Warehouse>> _logger;
	

		public WareHouseRepository(AppDbContext context, ILogger<MainRepository<Warehouse>> logger) : base( context, logger)
		{
			_context = context;
			_logger = logger;
			_warehouses = context.Warehouses;
		}

		public async Task<Warehouse?> GetByNameAsync(string Name)
		{
			_logger.LogInformation($"Executing {nameof(GetByNameAsync)} for Name: {Name}");
			Warehouse? warehouse = await _warehouses
				.Where(w => w.Name.Equals(Name) && w.DeletedAt == null)
				.Include(w => w.ProductInventories.Where(pi => pi.DeletedAt == null))
				.ThenInclude(pi => pi.Product)
				.SingleOrDefaultAsync();
			if (warehouse is null)
			{
				_logger.LogWarning($"No Warehouse with this Name:{Name}");
				return null;
			}
			_logger.LogInformation("Warehouse found in database");
			return warehouse;
		}


	}
}
