using ApplicationLayer.DtoModels.CategoryDtos;
using ApplicationLayer.DtoModels.ImagesDtos;
using ApplicationLayer.DtoModels.SubCategorydto;
using ApplicationLayer.Interfaces;
using Hangfire;
using Microsoft.AspNetCore.JsonPatch.Internal;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Linq.Expressions;

namespace ApplicationLayer.Services.CategoryServices
{
	public class CategoryQueryService : ICategoryQueryService
	{
		private readonly ILogger<CategoryQueryService> _logger;
		private readonly ICategoryCacheHelper _categoryCacheHelper;	
		private readonly IUnitOfWork _unitOfWork;
		private readonly ICategoryMapper _categoryMapper;
		public CategoryQueryService(ICategoryMapper categoryMapper, IUnitOfWork unitOfWork, 
			ILogger<CategoryQueryService> logger, ICategoryCacheHelper categoryCacheHelper)
		{
			_categoryMapper = categoryMapper;
			_unitOfWork = unitOfWork;
			_categoryCacheHelper = categoryCacheHelper;
			_logger = logger;
		}
		private IQueryable<DomainLayer.Models.Category> BasicFilter(IQueryable<DomainLayer.Models.Category> query, bool? isActive = null, bool? isDeleted = null,bool IsAdmin=false)
		{
			if(!IsAdmin)
				return query.Where(c=>c.IsActive&&c.DeletedAt==null);
			if (isActive.HasValue)
				query = query.Where(c => c.IsActive == isActive.Value);
			if (isDeleted.HasValue)
			{
				if (isDeleted.Value)
					query = query.Where(c => c.DeletedAt != null);
				else
					query = query.Where(c => c.DeletedAt == null);
			}
			return query;
		}

		private async Task<Result<List<CategoryDto>>> categoryDtos(string? word, bool? isActive = null, bool? isDeleted = null, int page = 1, int pageSize = 10,bool IsAdmin=false)
		{

			var query = _unitOfWork.Category.GetAll();
			query = BasicFilter(query, isActive, isDeleted,IsAdmin);
			if (!string.IsNullOrWhiteSpace(word))
			{
				query = query.Where(c => EF.Functions.Like(c.Name, $"%{word}%") || EF.Functions.Like(c.Description, $"%{word}%"));
			}
			 query =  query.Skip((page - 1) * pageSize)
			.Take(pageSize);
			var result = await _categoryMapper.CategorySelector(query).ToListAsync();
			_categoryCacheHelper.SetCategoryListCacheAsync(result, word, isActive, isDeleted, page, pageSize, IsAdmin);
			return Result<List<CategoryDto>>.Ok(result, "Categories fetched", 200);
		}

		
		public async Task<Result<List<CategoryDto>>> FilterAsync(string? search, bool? isActive, bool? isDeleted, int page, int pageSize,bool IsAdmin=false)
		{
			_logger.LogInformation($"Executing {nameof(FilterAsync)} with filters");
			var cached = await _categoryCacheHelper.GetCategoryListCacheAsync<List<CategoryDto>>(search, isActive, isDeleted, page, pageSize, IsAdmin);
			if (cached != null)
			{
				_logger.LogInformation($"Cache hit for FilterAsync ");
				return Result<List<CategoryDto>>.Ok(cached, "Categories fetched from cache", 200);
			}

			return await categoryDtos(search, isActive, isDeleted, page, pageSize, IsAdmin);
		}

		private async Task<CategorywithdataDto?> privateGetCategoryByIdAsync(int id, bool? isActive = null, bool? isDeleted = null,bool IsAdmin=false)
		{
			_logger.LogInformation($"Executing {nameof(GetCategoryByIdAsync)} for id: {id}");

			if(!IsAdmin)
			{
				isActive = true;
				isDeleted = false;
			}
			var query = _unitOfWork.Category.GetAll();

			query = query.Where(c => c.Id == id);

			query = BasicFilter(query, isActive, isDeleted, IsAdmin);

			var category = await _categoryMapper.CategorySelectorWithData(query,IsAdmin)
				.FirstOrDefaultAsync();

			if (category == null)
			{
				_logger.LogWarning($"Category with id: {id} doesn't exist");
				return null;
			}

			_logger.LogInformation($"Category with id: {id} exists");
			return category;
		}

		public async Task<Result<CategorywithdataDto>> GetCategoryByIdAsync(int id, bool? isActive = null, bool? IsDeleted = false,bool IsAdmin=false)
		{
			_logger.LogInformation($"Execute: {nameof(GetCategoryByIdAsync)} in services for id: {id}, isActive: {isActive}, includeDeleted: {IsDeleted}");

			var cachedCategory = await _categoryCacheHelper.GetCategoryByIdCacheAsync<CategorywithdataDto>(id, isActive, IsDeleted,IsAdmin);
			if (cachedCategory != null)
			{
				_logger.LogInformation($"Cache hit for category {id} with filters");
				return Result<CategorywithdataDto>.Ok(cachedCategory, "Category found in cache", 200);
			}

			var categoryDto = await privateGetCategoryByIdAsync(id, isActive, IsDeleted,IsAdmin);

			if (categoryDto == null)
			{
				_logger.LogWarning($"Category with id: {id} not found");
				return Result<CategorywithdataDto>.Fail($"Category with id: {id} not found", 404);
			}

			 _categoryCacheHelper.SetCategoryByIdCacheAsync(id, isActive, IsDeleted, categoryDto,IsAdmin);
			return Result<CategorywithdataDto>.Ok(categoryDto, "Category found", 200);
		}
	}
}


