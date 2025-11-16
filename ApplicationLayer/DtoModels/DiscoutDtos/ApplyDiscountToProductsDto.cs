namespace ApplicationLayer.DtoModels.DiscoutDtos
{
	public class ApplyDiscountToProductsDto
	{
		public int Discountid { get; set; }
		public List<int> ProductsId { get; set; }=new List<int>();
	}
}


