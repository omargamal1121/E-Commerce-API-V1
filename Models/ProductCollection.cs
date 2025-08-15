namespace E_Commerce.Models
{
    public class ProductCollection: BaseEntity
	{
        public int ProductId { get; set; }
        public Product Product { get; set; }
        public int CollectionId { get; set; }
        public Collection Collection { get; set; }
    }
} 