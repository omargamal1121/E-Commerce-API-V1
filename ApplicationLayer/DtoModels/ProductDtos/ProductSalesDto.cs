namespace ApplicationLayer.DtoModels.ProductDtos
{
    public class VariantSalesDto
    {
        public int VariantId { get; set; }
        public string Color { get; set; } = string.Empty;
        public string? Size { get; set; }
        public int? Waist { get; set; }
        public int? Length { get; set; }
        public int TotalSold { get; set; }
        public int RemainingQuantity { get; set; }
	}

    public class ProductSalesDto
    {
        public int ProductId { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public int TotalSold { get; set; }
        public IEnumerable<VariantSalesDto> VariantSales { get; set; } = new List<VariantSalesDto>();
    }
}
