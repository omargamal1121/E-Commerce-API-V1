using E_Commerce.Context;
using E_Commerce.UOW;
using Microsoft.EntityFrameworkCore;

namespace E_Commerce.Services.HardDeleteServices
{
	public interface IHardDeleteService
	{
		Task HardDeleteOldDataAsync();
	}
	public class CategoryHardDeleteService
	{
		private readonly IUnitOfWork _unitOfWork;
		public CategoryHardDeleteService(IUnitOfWork unitOfWorktext )
		{
			_unitOfWork=unitOfWorktext;
		}

		//public async Task HardDeleteOldDataAsync()
		//{
		//	var cutoffDate = DateTime.UtcNow.AddMonths(-1);

			
		//	var subCategories = await _context.SubCategories
		//		.Where(sc => sc.IsDeleted && sc.DeletedAt <= cutoffDate)
		//		.ToListAsync();
		//	_context.SubCategories.RemoveRange(subCategories);

		//	var categories = await _context.Categories
		//		.Where(c => c.IsDeleted && c.DeletedAt <= cutoffDate)
		//		.ToListAsync();
		//	_context.Categories.RemoveRange(categories);

		//	await _context.SaveChangesAsync();
		//}

	}
}
