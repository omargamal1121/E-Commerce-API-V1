using E_Commerce.Services;
using E_Commerce.Models;
using StackExchange.Redis;
using System.Linq.Expressions;

namespace E_Commerce.Interfaces
{
	public interface IRepository<T> where T : BaseEntity
	{
		Task<T?> CreateAsync(T model);
		Task<bool> SoftDeleteAsync(int id);
		public  Task<T[]> CreateRangeAsync(params T[] models);
		Task<bool> IsDeletedAsync(int id);
		Task<T?> GetByQuery(Expression<Func<T, bool>> predicate);
		bool Update(T model);
		void UpdateList(List<T> model);
		bool Remove(T model);
		void RemoveList(List<T> model);
		Task<bool> IsExsistAsync(int id);
		Task<T?> GetByIdAsync(int id);
		IQueryable<T> GetAll();
		Task<List<T>> GetAllDeletedAsync();
		Task<bool> RestoreAsync(int id);
	}
}
