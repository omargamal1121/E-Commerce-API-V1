namespace E_Commerce.Models
{
    public class WishlistItem:BaseEntity
    {
        public int Id { get; set; }
        public int ProductId { get; set; }
        public Product Product { get; set; }
        public string CustomerId { get; set; }
        public Customer Customer { get; set; }
    }
} 