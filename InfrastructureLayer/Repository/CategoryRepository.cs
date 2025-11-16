
using ApplicationLayer.Interfaces;
using DomainLayer.Models;
using InfrastructureLayer.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace InfrastructureLayer.Repository
{ 
public class CategoryRepository : MainRepository<Category>, ICategoryRepository
{
	private readonly DbSet<Category> _categories;
	private readonly ILogger<CategoryRepository> _logger;

	public CategoryRepository(AppDbContext context, ILogger<CategoryRepository> logger) : base(context, logger)
	{
		_categories = context.Categories;
		_logger = logger;
	}


	


	public async Task<bool> IsExsistsByNameAsync(string name)
	{
		_logger.LogInformation($"Executing {nameof(IsExsistsByNameAsync)} for name: {name}");

		var exists = await  _categories.AnyAsync(c => c.Name == name);

		if (exists)
			_logger.LogInformation($"Category with name: {name} already exists");
		else
			_logger.LogInformation($"Category with name: {name} does not exist");

		return exists;
	}

	public async Task<bool> ActiveCategoryAsync(int categoryId)
	{
		_logger.LogInformation("Activating category with ID: {CategoryId}", categoryId);

		var category = await _categories.FirstOrDefaultAsync(c => c.Id == categoryId && !c.IsActive);
		if (category == null)
		{
			_logger.LogWarning("Category {CategoryId} not found or already active", categoryId);
			return false;
		}

		category.IsActive = true;
		category.ModifiedAt = DateTime.UtcNow;

		_logger.LogInformation("Category {CategoryId} set to active. Saving...", categoryId);

		return true;
	}
	public async Task<bool> IsActiveAsync(int categoryId)
	{
		return await _categories.AnyAsync(c => c.Id == categoryId && c.IsActive);
	}
	public async Task<bool> IsDeactiveAsync(int categoryId)
	=> await _categories.AnyAsync(c => c.Id == categoryId && !c.IsActive);



	public async Task<bool> DeactiveCategoryAsync(int categoryId)
	{
		_logger.LogInformation("Deactivating category with ID: {CategoryId}", categoryId);

		var category = await _categories.FirstOrDefaultAsync(c => c.Id == categoryId && c.IsActive);
		if (category == null)
		{
			_logger.LogWarning("Category {CategoryId} not found or already inactive", categoryId);
			return false;
		}

		category.IsActive = false;
		category.ModifiedAt = DateTime.UtcNow;

		_logger.LogInformation("Category {CategoryId} set to inactive. Saving...", categoryId);
	

		return true;
	}

	public async Task<bool>HasImagesAsync(int categoryId)
		=> await _categories.AnyAsync(c => c.Id == categoryId && c.Images.Any(i=>i.DeletedAt==null));

	public async Task<bool>HasImageWithIdAsync(int categoryId,int imageid)
		=> await _categories.AnyAsync(c => c.Id == categoryId && c.Images.Any(i=>i.DeletedAt==null&&i.Id==imageid));


	public async Task<bool> HasSubCategoriesAsync(int categoryId)
	{
		return await _categories.AnyAsync(c => c.Id == categoryId && c.SubCategories.Any());
	}
	public async Task<bool> HasSubCategoriesActiveAsync(int categoryId)
	{
		return await _categories.AnyAsync(c => c.Id == categoryId && c.SubCategories.Any(sc=>sc.IsActive&&sc.DeletedAt==null));
	}
}

}
