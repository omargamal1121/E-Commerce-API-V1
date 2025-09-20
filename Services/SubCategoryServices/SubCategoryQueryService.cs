using AutoMapper;
using E_Commerce.DtoModels.CategoryDtos;
using E_Commerce.DtoModels.ImagesDtos;
using E_Commerce.DtoModels.ProductDtos;
using E_Commerce.DtoModels.SubCategorydto;
using E_Commerce.Enums;
using E_Commerce.Interfaces;
using E_Commerce.Models;
using E_Commerce.Services.AdminOpreationServices;
using E_Commerce.Services.Cache;
using E_Commerce.Services.EmailServices;
using E_Commerce.UOW;
using Hangfire;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Linq.Expressions;

namespace E_Commerce.Services.SubCategoryServices
{
    public class SubCategoryQueryService : ISubCategoryQueryService
    {
        private readonly ILogger<SubCategoryQueryService> _logger;
        private readonly IUnitOfWork _unitOfWork;
        private readonly ISubCategoryCacheHelper _subCategoryCacheHelper;
        private readonly ISubCategoryMapper _subCategoryMapper;

        public SubCategoryQueryService(
            ILogger<SubCategoryQueryService> logger,
            IUnitOfWork unitOfWork,
            ISubCategoryCacheHelper subCategoryCacheHelper,
            ISubCategoryMapper subCategoryMapper)
        {
            _logger = logger;
            _unitOfWork = unitOfWork;
            _subCategoryCacheHelper = subCategoryCacheHelper;
            _subCategoryMapper = subCategoryMapper;
        }

 

        public async Task<Result<SubCategoryDtoWithData>> GetSubCategoryByIdAsync(int id, bool? isActive = null, bool? isDeleted = null,bool IsAdmin=false)
        {
            _logger.LogInformation($"Execute: {nameof(GetSubCategoryByIdAsync)} in services for id: {id}, isActive: {isActive}, isDeleted: {isDeleted}");

            if(!IsAdmin)
            {
                isActive = true;
                isDeleted = false;
            }
         
            var cached = await _subCategoryCacheHelper.GetSubCategoryByIdCacheAsync<SubCategoryDtoWithData>(id, isActive, isDeleted, IsAdmin);
            if (cached != null)
            {
                _logger.LogInformation($"Cache hit for subcategory {id} with filters");
                return Result<SubCategoryDtoWithData>.Ok(cached, "SubCategory found in cache", 200);
            }


            var query = _unitOfWork.SubCategory.GetAll();

            query = query.Where(c => c.Id == id);

            query = BasicFilter(query, isActive, isDeleted,IsAdmin);
           

            var subCategory =await _subCategoryMapper.SubCategorySelectorWithData(query,IsAdmin)
				.FirstOrDefaultAsync();

            if (subCategory == null)
            {
                _logger.LogWarning($"SubCategory with id: {id} not found");
                return Result<SubCategoryDtoWithData>.Fail($"SubCategory with id: {id} not found", 404);
            }
            _subCategoryCacheHelper.SetSubCategoryByIdCacheAsync(id, isActive, isDeleted, subCategory, IsAdmin, TimeSpan.FromMinutes(30));

			return Result<SubCategoryDtoWithData>.Ok(subCategory, "SubCategory found", 200);
        }

		public async Task<Result<List<SubCategoryDto>>> FilterAsync(
	  string? search, bool? isActive = null, bool? isDeleted = null, int page = 1, int pageSize = 10,bool IsAdmin=false)
		{
			_logger.LogInformation($"Executing {nameof(FilterAsync)} with filters");

			var cachedData = await _subCategoryCacheHelper
				.GetSubCategoryListCacheAsync<List<SubCategoryDto>>(search, isActive, isDeleted, page, pageSize, IsAdmin);

			if (cachedData != null)
				return Result<List<SubCategoryDto>>.Ok(cachedData, "Subcategories from cache", 200);

			var query = _unitOfWork.SubCategory.GetAll();

			if (!string.IsNullOrWhiteSpace(search))
			{
				query = query.Where(sc =>
					EF.Functions.Like(sc.Name, $"%{search}%") ||
					EF.Functions.Like(sc.Description, $"%{search}%"));
			}

			query = BasicFilter(query, isActive, isDeleted,IsAdmin);

			var subCategoryDtos = await _subCategoryMapper.SubCategorySelector(query)
				.OrderBy(sc => sc.Id)
				.Skip((page - 1) * pageSize)
				.Take(pageSize)
				.ToListAsync();

			if (!subCategoryDtos.Any())
				return Result<List<SubCategoryDto>>.Fail("No subcategories found", 404);

			_subCategoryCacheHelper.SetSubCategoryListCacheAsync(subCategoryDtos, search, isActive, isDeleted, page, pageSize, IsAdmin, TimeSpan.FromMinutes(30));
            
            return Result<List<SubCategoryDto>>.Ok(subCategoryDtos, "Filtered subcategories retrieved", 200);
        }

		private IQueryable<SubCategory> BasicFilter(IQueryable<SubCategory> query, bool? isActive, bool? isdelete, bool IsAdmin = false)
        {
            if(!IsAdmin)
            {
                isActive = true;
                isdelete = false;
            }
            if (isActive.HasValue)
            {
                if (isActive.Value)
                    query = query.Where(p => p.IsActive);
                else
                    query = query.Where(p => !p.IsActive);
            }
            if (isdelete.HasValue)
            {
                if (isdelete.Value)
                    query = query.Where(p => p.DeletedAt != null);
                else
                    query = query.Where(p => p.DeletedAt == null);
            }
            return query;
        }
    }
}

