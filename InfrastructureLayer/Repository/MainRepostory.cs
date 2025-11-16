
using ApplicationLayer.Interfaces;
using DomainLayer.Models;
using InfrastructureLayer.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using StackExchange.Redis;
using System.Linq.Expressions;
using static Microsoft.EntityFrameworkCore.DbLoggerCategory;

namespace InfrastructureLayer.Repository
{



	public class MainRepository<T> : IRepository<T> where T : BaseEntity
	{
		private readonly AppDbContext _context;
		private readonly DbSet<T> _entities;
		private readonly ILogger<MainRepository<T>> _logger;

		public MainRepository(AppDbContext context, ILogger<MainRepository<T>> logger)
		{

			_context = context ?? throw new ArgumentNullException(nameof(context));
			_logger = logger ?? throw new ArgumentNullException(nameof(logger));
			_entities = _context.Set<T>();
		}

		public async Task<T?> CreateAsync(T model)
		{
			_logger.LogInformation($"Executing {nameof(CreateAsync)} for entity {typeof(T).Name}");

			if (model == null)
			{
				_logger.LogWarning("CreateAsync called with null model");
				return null;
			}

			await _entities.AddAsync(model);
			_logger.LogInformation($"{typeof(T).Name} added successfully (pending save)");
			return model;
		}
		public async Task<T[]> CreateRangeAsync(params T[] models)
		{
			_logger.LogInformation($"Executing {nameof(CreateRangeAsync)} for entity {typeof(T).Name}");

			if (models == null || models.Length == 0)
			{
				_logger.LogWarning($"{nameof(CreateRangeAsync)} called with null or empty models");
				return Array.Empty<T>();
			}

			await _entities.AddRangeAsync(models);
			_logger.LogInformation($"{models.Length} {typeof(T).Name} entities added successfully (pending save)");

			return models;
		}



		public IQueryable<T> GetAll()
		{
			_logger.LogInformation($"Execute {nameof(GetAll)} for entity {typeof(T).Name}");
			return _entities;
		}

		public bool Remove(T model)
		{
			_logger.LogInformation($"Execute {nameof(Remove)} for entity {typeof(T).Name}");

			if (model == null)
			{
				_logger.LogWarning("RemoveAsync called with null model");
				return false;
			}


			_entities.Remove(model);

			_logger.LogInformation($"{typeof(T).Name} marked for deletion (pending save)");
			return true;
		}

		public async Task<T?> GetByIdAsync(int id)
		{
			_logger.LogInformation($"Executing {nameof(GetByIdAsync)} for entity {typeof(T).Name} with ID: {id}");
			var entity = await _entities
				.FirstOrDefaultAsync(e => e.Id == id);

			if (entity == null)
				_logger.LogWarning($"{typeof(T).Name} with ID {id} not found or deleted");

			return entity;
		}

		public bool Update(T model)
		{
			_logger.LogInformation($"Execute {nameof(Update)} for entity {typeof(T).Name}");




			model.ModifiedAt = DateTime.UtcNow;
			var isupdated = _context.Update(model);
			if (isupdated == null)
				return false;




			_logger.LogInformation($"{typeof(T).Name} marked for update (pending save)");
			return true;
		}
		public void UpdateList(List<T> model)
		{
			_logger.LogInformation($"Execute {nameof(Update)} for entity {typeof(T).Name}");
			_context.UpdateRange(model);
			_logger.LogInformation($"{typeof(T).Name} marked for update (pending save)");
		}

		public async Task<T?> GetByQuery(Expression<Func<T, bool>> predicate)
		{
			try
			{
				var entity = await _entities.FirstOrDefaultAsync(predicate);
				return entity;
			}
			catch (Exception ex)
			{
				_logger.LogError($"Error retrieving {typeof(T).Name}: {ex.Message}");
				return null;
			}
		}

		public async Task<bool> SoftDeleteAsync(int id)
		{
			_logger.LogInformation($"Execute {nameof(SoftDeleteAsync)} for entity {typeof(T).Name} with ID: {id}");

			var entity = await _entities.FirstOrDefaultAsync(e => e.Id == id && e.DeletedAt == null);
			if (entity == null)
			{
				_logger.LogWarning($"{typeof(T).Name} with ID: {id} not found or already deleted.");
				return false;
			}

			entity.DeletedAt = DateTime.UtcNow;
			_entities.Update(entity);

			_logger.LogInformation($"{typeof(T).Name} with ID: {id} soft deleted.");
			return true;
		}

		public async Task<bool> IsExsistAsync(int id)
		{
			return await _entities.AnyAsync(e => e.Id == id && e.DeletedAt == null);
		}

		public async Task<bool> IsDeletedAsync(int id)
		{
			return await _entities.AnyAsync(e => e.Id == id && e.DeletedAt != null);
		}

		public async Task<List<T>> GetAllDeletedAsync()
		{
			_logger.LogInformation($"Execute {nameof(GetAllDeletedAsync)} for entity {typeof(T).Name}");
			return await _entities.AsNoTracking().Where(e => e.DeletedAt != null).ToListAsync();
		}

		public async Task<bool> RestoreAsync(int id)
		{
			_logger.LogInformation($"Execute {nameof(RestoreAsync)} for entity {typeof(T).Name} with ID: {id}");
			var entity = await _entities.FirstOrDefaultAsync(e => e.Id == id && e.DeletedAt != null);
			if (entity == null)
			{
				_logger.LogWarning($"{typeof(T).Name} with ID: {id} not found or not deleted.");
				return false;
			}
			entity.DeletedAt = null;

			_logger.LogInformation($"{typeof(T).Name} with ID: {id} restored.");
			return true;
		}

		public void RemoveList(List<T> models)
		{
			_entities.RemoveRange(models);
		}
	}
}