namespace E_Commerce.DtoModels.ProductDtos
{
	public class BestSellingProductDto
    {
        public int ProductId { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public string? Image { get; set; }
        public int TotalSoldQuantity { get; set; }
    }
}
