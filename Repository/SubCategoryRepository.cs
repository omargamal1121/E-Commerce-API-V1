using E_Commerce.Context;
using E_Commerce.DtoModels.SubCategorydto;
using E_Commerce.Interfaces;
using E_Commerce.Models;
using E_Commerce.UOW;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using static Microsoft.EntityFrameworkCore.DbLoggerCategory;
using static Microsoft.Extensions.Logging.EventSource.LoggingEventSource;

public class SubCategoryRepository : MainRepository<SubCategory>, ISubCategoryRepository
{
    private readonly DbSet<SubCategory> _subCategories;
    private readonly ILogger<SubCategoryRepository> _logger;

    public SubCategoryRepository(AppDbContext context, ILogger<SubCategoryRepository> logger) : base(context, logger)
    {
        _subCategories = context.Set<SubCategory>();
        _logger = logger;
    }


	
	public async Task<bool> IsExsistAndActive(int id)
	{
		return await _subCategories.AnyAsync(sc => sc.Id == id && sc.IsActive && sc.DeletedAt == null);
	}
	public async Task<bool> IsExsistAndDeActive(int id)
	{
		return await _subCategories.AnyAsync(sc => sc.Id == id && !sc.IsActive && sc.DeletedAt != null);
	}

	public async Task<bool> ActiveSubCategoryAsync(int id)
	{
		var subcategory=await GetByIdAsync(id);
		if(subcategory==null||subcategory.IsActive) return false;
		subcategory.IsActive = true;
		return true;
	}
	public async Task<bool> DeActiveSubCategoryAsync(int id)
	{
		var subcategory=await GetByIdAsync(id);
		if(subcategory==null||subcategory.IsActive) return false;
		subcategory.IsActive = false;
		return true;
	}

	
	public async Task<bool> IsHasActiveProduct(int subCategoryId)
	{
		return await GetAll()
			.AsNoTracking()
			.AnyAsync(sc => sc.Id == subCategoryId && sc.Products.Any(p => p.IsActive && p.DeletedAt == null));
	}


	

	public async Task<bool> IsExsistByNameAsync(string name)
	{

		_logger.LogInformation($"Executing {nameof(IsExsistByNameAsync)} for name: {name}");

		var exists = await _subCategories.AnyAsync(c => c.Name == name);

		if (exists)
			_logger.LogInformation($"Category with name: {name} already exists");
		else
			_logger.LogInformation($"Category with name: {name} does not exist");

		return exists;

	}

	public async Task<bool> HasImagesAsync(int subCategoryId)
	{
		return await _subCategories.AnyAsync(sc => sc.Id == subCategoryId && sc.Images.Any(i=>i.DeletedAt==null));
	}
	public async Task<bool> HasProductsAsync(int subCategoryId)
    {
        return await _subCategories.AnyAsync(sc => sc.Id == subCategoryId && sc.Products.Any());
    }
} 