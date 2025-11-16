


using DomainLayer.Enums;
using DomainLayer.Models;

namespace ApplicationLayer.DtoModels.ProductDtos
{
	public class AdvancedSearchDto
    {
        public string? SearchTerm { get; set; }
        public int? Subcategoryid { get; set; }
        public Gender? Gender { get; set; }
        public FitType? FitType { get; set; }
        public decimal? MinPrice { get; set; }
        public decimal? MaxPrice { get; set; }
        public bool? InStock { get; set; }
        public bool? OnSale { get; set; }
        public string? SortBy { get; set; }
        public bool SortDescending { get; set; } = false;
        public string? Color { get; set; } // Filter by variant color
        public decimal? MinSize { get; set; } // Minimum variant size
        public decimal? MaxSize { get; set; } // Maximum variant size
    }
}


